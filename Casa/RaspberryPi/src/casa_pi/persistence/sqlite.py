import json
import sqlite3


class Store:
    def __init__(self, path, max_events=1000):
        self.max_events = max_events
        self.db = sqlite3.connect(str(path), timeout=10)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("PRAGMA synchronous=FULL")
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS commands (
              id TEXT PRIMARY KEY, body TEXT NOT NULL, status TEXT NOT NULL,
              result TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE IF NOT EXISTS events (
              seq INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL,
              delivered INTEGER NOT NULL DEFAULT 0);
        ''')
        # A crash may have occurred after the physical effect. Never retry it.
        with self.db:
            self.db.execute("UPDATE commands SET status='unknown', result=? WHERE status='executing'",
                            (json.dumps({"status": "unknown", "reason": "reinicio durante ejecucion"}),))

    def get(self, key, default=None):
        row = self.db.execute("SELECT value FROM state WHERE key=?", (key,)).fetchone()
        return json.loads(row[0]) if row else default

    def set(self, key, value):
        with self.db:
            self.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", (key, json.dumps(value)))

    def claim(self, command):
        with self.db:
            return self.db.execute("INSERT OR IGNORE INTO commands(id,body,status) VALUES (?,?,'executing')",
                                   (command["commandId"], json.dumps(command, sort_keys=True))).rowcount == 1

    def result(self, command_id):
        row = self.db.execute("SELECT status,result,body FROM commands WHERE id=?", (command_id,)).fetchone()
        return {"status": row[0], "result": json.loads(row[1]) if row[1] else None,
                "command": json.loads(row[2])} if row else None

    def finish(self, command_id, result):
        with self.db:
            self.db.execute("UPDATE commands SET status=?,result=? WHERE id=?",
                            (result["status"], json.dumps(result), command_id))

    def emit(self, body):
        with self.db:
            self.db.execute("INSERT INTO events(body) VALUES (?)", (json.dumps(body),))
            # Prefer dropping periodic summaries over security events.
            count = self.db.execute("SELECT COUNT(*) FROM events").fetchone()[0]
            excess = count - self.max_events
            if excess > 0:
                self.db.execute("DELETE FROM events WHERE seq IN (SELECT seq FROM events ORDER BY delivered DESC, CASE WHEN body LIKE '%TelemetryReported%' THEN 0 ELSE 1 END, seq LIMIT ?)", (excess,))

    def pending(self, limit=20):
        return [(seq, json.loads(body)) for seq, body in self.db.execute(
            "SELECT seq,body FROM events WHERE delivered=0 ORDER BY seq LIMIT ?", (limit,))]

    def delivered(self, seq):
        with self.db:
            self.db.execute("UPDATE events SET delivered=1 WHERE seq=?", (seq,))

    def close(self):
        self.db.close()

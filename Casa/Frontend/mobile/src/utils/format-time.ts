const pad = (value: number) => String(value).padStart(2, '0');

/** "18:42" */
export function formatTime(date: Date) {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** "hoy, 18:42" o "27/10, 18:42" */
export function formatReportTime(date: Date, now = new Date()) {
  const isToday = date.toDateString() === now.toDateString();
  const day = isToday ? 'hoy' : `${pad(date.getDate())}/${pad(date.getMonth() + 1)}`;
  return `${day}, ${formatTime(date)}`;
}

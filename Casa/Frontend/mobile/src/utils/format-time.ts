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

const MONTHS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

/** "Hoy · 23 oct", "Ayer · 22 oct" o "21 oct" */
export function formatDayLabel(date: Date, now = new Date()) {
  const dayMonth = `${date.getDate()} ${MONTHS[date.getMonth()]}`;
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (date.toDateString() === now.toDateString()) return `Hoy · ${dayMonth}`;
  if (date.toDateString() === yesterday.toDateString()) return `Ayer · ${dayMonth}`;
  return dayMonth;
}

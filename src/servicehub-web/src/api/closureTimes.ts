export function closureInput(startDate: string, startTime: string, endDate: string, endTime: string, reason: string) {
  const localTimestamp = (date: string, time: string) => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || !/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) {
      throw new Error('Choose a start date and time and an end date and time in your business time zone.')
    }
    const input = `${date}T${time}:00.000Z`
    const parsed = new Date(input)
    if (!Number.isFinite(parsed.getTime()) || parsed.toISOString() !== input || parsed.getUTCFullYear() < 2000 || parsed.getUTCFullYear() > 2100) {
      throw new Error('Choose valid closure dates between 2000 and 2100.')
    }
    // UTC is used only to check calendar validity, never to convert the owner's local input.
    return `${date}T${time}`
  }
  const startLocal = localTimestamp(startDate, startTime)
  const endLocal = localTimestamp(endDate, endTime)
  if (endLocal <= startLocal) throw new Error('Closure end must be after its start.')
  return { startLocal, endLocal, reason: reason.trim() || null }
}

import test from 'node:test'
import assert from 'node:assert/strict'
import { closureInput } from '../src/api/closureTimes.ts'

test('custom dates and minute-level times are preserved as business-local values', () => {
  assert.deepEqual(closureInput('2030-01-07', '01:25', '2030-01-07', '03:40', ' Maintenance '), {
    startLocal: '2030-01-07T01:25', endLocal: '2030-01-07T03:40', reason: 'Maintenance',
  })
})

test('closures can span midnight with an optional reason', () => {
  assert.deepEqual(closureInput('2030-01-07', '23:45', '2030-01-08', '00:15', '  '), {
    startLocal: '2030-01-07T23:45', endLocal: '2030-01-08T00:15', reason: null,
  })
})

test('equal or reversed timestamps are rejected', () => {
  for (const [date, time] of [['2030-01-07', '09:15'], ['2030-01-07', '09:00'], ['2030-01-06', '20:00']]) {
    assert.throws(() => closureInput('2030-01-07', '09:15', date, time, ''), /end must be after/)
  }
})

test('missing times and invalid dates are rejected rather than defaulting to midnight', () => {
  for (const [date, time] of [['2030-01-07', ''], ['2030-02-30', '09:00'], ['', '09:00'], ['2030-01-07', '24:00']]) {
    assert.throws(() => closureInput(date, time, '2030-03-01', '10:00', ''))
  }
})

test('business-local input is unchanged by the browser timezone', () => {
  const previous = process.env.TZ
  try {
    process.env.TZ = 'America/New_York'
    const input = closureInput('2030-01-07', '01:25', '2030-01-07', '03:40', '')
    assert.equal(input.startLocal, '2030-01-07T01:25')
    assert.equal(input.endLocal, '2030-01-07T03:40')
  } finally {
    if (previous === undefined) delete process.env.TZ
    else process.env.TZ = previous
  }
})

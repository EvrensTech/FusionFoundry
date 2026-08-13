'use strict';

const assert = require('assert');
const domain = require('./duel-domain');

const record = {
  matchId: 'match-1', ticketId: 'ticket-1', nonce: 'nonce-1', mode: 'Ranked',
  playerOneId: 'one', playerTwoId: 'two', playerOneScore: 3, playerTwoScore: 1,
  winnerIndex: 0, terminationReason: 'ScoreLimit',
  startedUnixSeconds: 100, endedUnixSeconds: 200
};
const ticket = {
  ticketId: 'ticket-1', playerId: 'one', mode: 'Ranked', nonce: 'nonce-1',
  issuedUnixSeconds: 100, expiresUnixSeconds: 290
};

assert.strictEqual(domain.ratingAfter(1000, 1000, 1), 1016);
const first = domain.settle(domain.createProfile('one'), 1000, record, 'one', 200, ticket);
assert.strictEqual(first.result.ratingAfter, 1016);
assert.strictEqual(first.result.experienceAwarded, 100);
const duplicate = domain.settle(first.profile, 1000, record, 'one', 200, ticket);
assert.strictEqual(duplicate.result.alreadyProcessed, true);
assert.strictEqual(duplicate.profile.matchHistory.length, 1);

const createdTicket = domain.createTicket('one', first.profile,
  { mode: 'Ranked', buildId: 'build-1', platform: 'Windows' }, 200, () => 'id');
assert.strictEqual(createdTicket.rating, 1016);
assert.strictEqual(createdTicket.expiresUnixSeconds, 290);

assert.throws(() => domain.validateTicket(
  { ...createdTicket, nonce: 'changed' },
  { ...record, ticketId: createdTicket.ticketId, nonce: createdTicket.nonce },
  'one', 200), /INVALID_TICKET/);
assert.throws(() => domain.validateRecord(
  { ...record, playerOneScore: 0, playerTwoScore: 3, winnerIndex: 0 },
  'one', 200), /WINNER_SCORE_MISMATCH/);

const upgraded = domain.upgradeProfile(
  { schemaVersion: 1, playerId: 'legacy', rating: 1234, customField: 'preserved' },
  'legacy');
assert.strictEqual(upgraded.schemaVersion, 2);
assert.strictEqual(upgraded.rating, 1234);
assert.strictEqual(upgraded.customField, 'preserved');
assert.deepStrictEqual(upgraded.matchHistory, []);

console.log('DUEL_CLOUD_CODE_DOMAIN_TESTS_OK');

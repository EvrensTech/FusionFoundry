'use strict';

const INITIAL_RATING = 1000;
const K_FACTOR = 32;
const HISTORY_LIMIT = 20;
const CURRENT_SCHEMA_VERSION = 2;
const VALID_MODES = ['Ai', 'Private', 'Unranked', 'Ranked'];
const VALID_TERMINATIONS = [
  'ScoreLimit', 'TimeExpired', 'OvertimeScore', 'Draw',
  'ClientForfeit', 'HostLost', 'Cancelled'
];

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function ratingAfter(current, opponent, result) {
  const expected = 1 / (1 + Math.pow(10, (opponent - current) / 400));
  return Math.max(0, current + Math.round(K_FACTOR * (clamp(result, 0, 1) - expected)));
}

function leagueFor(rating) {
  if (rating < 900) return 'Bronze';
  if (rating < 1100) return 'Silver';
  if (rating < 1300) return 'Gold';
  return 'Platinum';
}

function createProfile(playerId) {
  return {
    schemaVersion: CURRENT_SCHEMA_VERSION,
    playerId,
    displayName: playerId,
    level: 1,
    experience: 0,
    rating: INITIAL_RATING,
    league: 'Silver',
    wins: 0,
    losses: 0,
    draws: 0,
    matchHistory: [],
    daily: null,
    processedMatchIds: [],
    preferences: { inputBindingsJson: '', reducedMotion: false, highContrast: false, hudScale: 1 }
  };
}

function upgradeProfile(profile, playerId) {
  const defaults = createProfile(playerId);
  const upgraded = { ...defaults, ...(profile || {}) };
  upgraded.schemaVersion = CURRENT_SCHEMA_VERSION;
  upgraded.playerId = upgraded.playerId || playerId;
  upgraded.matchHistory = Array.isArray(upgraded.matchHistory) ? upgraded.matchHistory : [];
  upgraded.processedMatchIds = Array.isArray(upgraded.processedMatchIds)
    ? upgraded.processedMatchIds
    : [];
  upgraded.preferences = { ...defaults.preferences, ...(upgraded.preferences || {}) };
  return upgraded;
}

function validateRecord(record, callerPlayerId, nowSeconds) {
  if (!record || !record.matchId || !record.ticketId || !record.nonce) {
    throw new Error('INVALID_MATCH_ID');
  }
  if (!record.playerOneId || !record.playerTwoId || record.playerOneId === record.playerTwoId) {
    throw new Error('INVALID_PARTICIPANTS');
  }
  if (record.playerOneId !== callerPlayerId && record.playerTwoId !== callerPlayerId) {
    throw new Error('CALLER_NOT_PARTICIPANT');
  }
  if (!Number.isInteger(record.playerOneScore) || !Number.isInteger(record.playerTwoScore) ||
      record.playerOneScore < 0 || record.playerTwoScore < 0 ||
      record.playerOneScore > 3 || record.playerTwoScore > 3) {
    throw new Error('INVALID_SCORE');
  }
  if (![-1, 0, 1].includes(record.winnerIndex)) throw new Error('INVALID_WINNER');
  if (!VALID_MODES.includes(record.mode) || !VALID_TERMINATIONS.includes(record.terminationReason)) {
    throw new Error('INVALID_RULES');
  }
  if (record.endedUnixSeconds < record.startedUnixSeconds ||
      record.endedUnixSeconds - record.startedUnixSeconds > 300 ||
      record.endedUnixSeconds > nowSeconds + 30) {
    throw new Error('INVALID_DURATION');
  }
  if ((record.winnerIndex === 0 && record.playerOneScore <= record.playerTwoScore) ||
      (record.winnerIndex === 1 && record.playerTwoScore <= record.playerOneScore) ||
      (record.winnerIndex < 0 && record.playerOneScore !== record.playerTwoScore &&
       !['HostLost', 'Cancelled'].includes(record.terminationReason)) ||
      (record.terminationReason === 'ScoreLimit' &&
       Math.max(record.playerOneScore, record.playerTwoScore) !== 3) ||
      (record.terminationReason === 'HostLost' && record.winnerIndex !== -1)) {
    throw new Error('WINNER_SCORE_MISMATCH');
  }
}

function validateTicket(ticket, record, callerPlayerId, nowSeconds) {
  if (!ticket || ticket.ticketId !== record.ticketId || ticket.playerId !== callerPlayerId ||
      ticket.mode !== record.mode || ticket.nonce !== record.nonce ||
      record.startedUnixSeconds < ticket.issuedUnixSeconds ||
      record.startedUnixSeconds > ticket.expiresUnixSeconds ||
      (ticket.consumedMatchId && ticket.consumedMatchId !== record.matchId)) {
    throw new Error('INVALID_TICKET');
  }
}

function settle(profile, opponentRating, record, callerPlayerId, nowSeconds, ticket) {
  validateRecord(record, callerPlayerId, nowSeconds);
  if (profile.processedMatchIds.includes(record.matchId)) {
    return { profile, result: { accepted: true, alreadyProcessed: true, status: 'Settled' } };
  }
  validateTicket(ticket, record, callerPlayerId, nowSeconds);
  ticket.consumedMatchId = record.matchId;

  const localIndex = record.playerOneId === callerPlayerId ? 0 : 1;
  const won = record.winnerIndex === localIndex;
  const draw = record.winnerIndex < 0;
  const before = profile.rating;
  if (record.mode === 'Ranked' && record.terminationReason !== 'HostLost') {
    profile.rating = ratingAfter(profile.rating, opponentRating, draw ? 0.5 : (won ? 1 : 0));
    profile.league = leagueFor(profile.rating);
  }

  const xp = record.mode === 'Ai' ? (won ? 50 : 20) : (won ? 100 : 40);
  profile.experience += xp;
  profile.level = Math.max(1, 1 + Math.floor(profile.experience / 500));
  if (draw) profile.draws += 1;
  else if (won) profile.wins += 1;
  else profile.losses += 1;
  profile.matchHistory.unshift(record);
  profile.matchHistory = profile.matchHistory.slice(0, HISTORY_LIMIT);
  profile.processedMatchIds.unshift(record.matchId);
  profile.processedMatchIds = profile.processedMatchIds.slice(0, 100);
  const utcDate = new Date(nowSeconds * 1000).toISOString().slice(0, 10);
  if (!profile.daily || profile.daily.utcDate !== utcDate) {
    profile.daily = { utcDate, matchesPlayed: 0, wins: 0, coreCarrySeconds: 0 };
  }
  profile.daily.matchesPlayed += 1;
  if (won) profile.daily.wins += 1;
  profile.daily.coreCarrySeconds += Math.max(0,
    localIndex === 0 ? (record.playerOneCarrySeconds || 0) : (record.playerTwoCarrySeconds || 0));

  return {
    profile,
    result: {
      accepted: true,
      alreadyProcessed: false,
      status: 'Settled',
      errorCode: '',
      ratingBefore: before,
      ratingAfter: profile.rating,
      experienceAwarded: xp
    }
  };
}

function createTicket(playerId, profile, args, nowSeconds, randomId) {
  if (!VALID_MODES.includes(args.mode)) throw new Error('INVALID_MODE');
  if (!args.buildId || !args.platform) throw new Error('INVALID_CLIENT');
  return {
    ticketId: randomId(),
    playerId,
    mode: args.mode,
    rating: profile.rating,
    buildId: args.buildId,
    platform: args.platform,
    issuedUnixSeconds: nowSeconds,
    expiresUnixSeconds: nowSeconds + 90,
    nonce: randomId()
  };
}

module.exports = {
  createProfile, createTicket, leagueFor, ratingAfter,
  settle, upgradeProfile, validateRecord, validateTicket
};

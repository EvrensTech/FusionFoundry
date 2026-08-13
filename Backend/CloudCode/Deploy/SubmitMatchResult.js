'use strict';

const { DataApi } = require('@unity-services/cloud-save-1.4');
const { LeaderboardsApi } = require('@unity-services/leaderboards-1.1');

const PROFILE_KEY = 'duel_profile';
const ACTIVE_TICKET_KEY = 'duel_active_ticket';
const LEADERBOARD_ID = 'duel-rating';
const CURRENT_SCHEMA_VERSION = 2;

function clamp(value, min, max) { return Math.max(min, Math.min(max, value)); }
function leagueFor(rating) {
  if (rating < 900) return 'Bronze';
  if (rating < 1100) return 'Silver';
  if (rating < 1300) return 'Gold';
  return 'Platinum';
}
function ratingAfter(current, opponent, result) {
  const expected = 1 / (1 + Math.pow(10, (opponent - current) / 400));
  return Math.max(0, current + Math.round(32 * (clamp(result, 0, 1) - expected)));
}
function createProfile(playerId) {
  return {
    schemaVersion: CURRENT_SCHEMA_VERSION, playerId, displayName: playerId, level: 1, experience: 0,
    rating: 1000, league: 'Silver', wins: 0, losses: 0, draws: 0,
    matchHistory: [], daily: null, processedMatchIds: [],
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
    ? upgraded.processedMatchIds : [];
  upgraded.preferences = { ...defaults.preferences, ...(upgraded.preferences || {}) };
  return upgraded;
}
function validateRecord(record, callerPlayerId, nowSeconds) {
  if (!record?.matchId || !record?.ticketId || !record?.nonce) throw new Error('INVALID_MATCH_ID');
  if (!record.playerOneId || !record.playerTwoId || record.playerOneId === record.playerTwoId) {
    throw new Error('INVALID_PARTICIPANTS');
  }
  if (record.playerOneId !== callerPlayerId && record.playerTwoId !== callerPlayerId) {
    throw new Error('CALLER_NOT_PARTICIPANT');
  }
  if (!Number.isInteger(record.playerOneScore) || !Number.isInteger(record.playerTwoScore) ||
      record.playerOneScore < 0 || record.playerTwoScore < 0 ||
      record.playerOneScore > 3 || record.playerTwoScore > 3) throw new Error('INVALID_SCORE');
  if (![-1, 0, 1].includes(record.winnerIndex)) throw new Error('INVALID_WINNER');
  if (!['Ai', 'Private', 'Unranked', 'Ranked'].includes(record.mode) ||
      !['ScoreLimit', 'TimeExpired', 'OvertimeScore', 'Draw', 'ClientForfeit',
        'HostLost', 'Cancelled'].includes(record.terminationReason)) {
    throw new Error('INVALID_RULES');
  }
  if (!Number.isInteger(record.startedUnixSeconds) || !Number.isInteger(record.endedUnixSeconds) ||
      record.endedUnixSeconds < record.startedUnixSeconds ||
      record.endedUnixSeconds - record.startedUnixSeconds > 300 ||
      record.endedUnixSeconds > nowSeconds + 30) throw new Error('INVALID_DURATION');
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
function recordsAgree(first, second) {
  return first.playerOneId === second.playerOneId && first.playerTwoId === second.playerTwoId &&
    first.playerOneScore === second.playerOneScore && first.playerTwoScore === second.playerTwoScore &&
    first.winnerIndex === second.winnerIndex &&
    first.terminationReason === second.terminationReason && first.mode === second.mode;
}
function settle(profile, opponentRating, record, callerPlayerId, nowSeconds) {
  validateRecord(record, callerPlayerId, nowSeconds);
  if (profile.processedMatchIds.includes(record.matchId)) {
    return { profile, result: { accepted: true, alreadyProcessed: true, status: 'Settled' } };
  }
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
  profile.matchHistory = profile.matchHistory.slice(0, 20);
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
      accepted: true, alreadyProcessed: false, status: 'Settled', errorCode: '',
      ratingBefore: before, ratingAfter: profile.rating, experienceAwarded: xp
    }
  };
}
async function getState(cloudSave, context, playerId) {
  const saved = await cloudSave.getProtectedItems(
    context.projectId, playerId, [PROFILE_KEY, ACTIVE_TICKET_KEY]);
  const profileItem = saved?.data?.results?.find(result => result.key === PROFILE_KEY);
  const ticketItem = saved?.data?.results?.find(result => result.key === ACTIVE_TICKET_KEY);
  return {
    profile: upgradeProfile(profileItem?.value, playerId),
    profileWriteLock: profileItem?.writeLock ?? null,
    ticket: ticketItem?.value ?? null,
    ticketWriteLock: ticketItem?.writeLock ?? null
  };
}

module.exports = async ({ params, context, logger }) => {
  if (!context.playerId) throw new Error('AUTHENTICATION_REQUIRED');
  const record = params.record;
  const nowSeconds = Math.floor(Date.now() / 1000);
  validateRecord(record, context.playerId, nowSeconds);
  const opponentId = record.playerOneId === context.playerId
    ? record.playerTwoId
    : record.playerOneId;
  const cloudSave = new DataApi(context);
  const local = await getState(cloudSave, context, context.playerId);
  const opponent = record.mode === 'Ai'
    ? { profile: createProfile(opponentId) }
    : await getState(cloudSave, context, opponentId);
  if (local.profile.processedMatchIds.includes(record.matchId)) {
    return { accepted: true, alreadyProcessed: true, status: 'Settled' };
  }
  validateTicket(local.ticket, record, context.playerId, nowSeconds);
  const opponentReport = opponent.profile.matchHistory.find(
    item => item.matchId === record.matchId);
  if (opponentReport && !recordsAgree(opponentReport, record)) {
    logger.warn('Conflicting Duel match reports', {
      matchId: record.matchId,
      playerId: context.playerId,
      opponentId
    });
    return {
      accepted: false,
      alreadyProcessed: false,
      status: 'Disputed',
      errorCode: 'CONFLICTING_REPORT'
    };
  }
  const settled = settle(
    local.profile, opponent.profile.rating, record, context.playerId, nowSeconds);
  if (!settled.result.alreadyProcessed) {
    const item = { key: PROFILE_KEY, value: settled.profile };
    if (local.profileWriteLock) item.writeLock = local.profileWriteLock;
    await cloudSave.setProtectedItem(context.projectId, context.playerId, item);
    const consumedTicket = { ...local.ticket, consumedMatchId: record.matchId };
    const ticketItem = { key: ACTIVE_TICKET_KEY, value: consumedTicket };
    if (local.ticketWriteLock) ticketItem.writeLock = local.ticketWriteLock;
    await cloudSave.setProtectedItem(context.projectId, context.playerId, ticketItem);
    const leaderboards = new LeaderboardsApi(context);
    await leaderboards.addLeaderboardPlayerScore(
      context.projectId, LEADERBOARD_ID, context.playerId, { score: settled.profile.rating });
  }
  logger.info('Duel match result settled', {
    playerId: context.playerId,
    matchId: record.matchId,
    alreadyProcessed: settled.result.alreadyProcessed
  });
  return settled.result;
};

module.exports.params = {
  record: { type: 'JSON', required: true }
};

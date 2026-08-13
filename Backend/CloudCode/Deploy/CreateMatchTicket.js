'use strict';

const { DataApi } = require('@unity-services/cloud-save-1.4');

const PROFILE_KEY = 'duel_profile';
const ACTIVE_TICKET_KEY = 'duel_active_ticket';
const CURRENT_SCHEMA_VERSION = 2;

function randomHex(byteCount) {
  let value = '';
  for (let index = 0; index < byteCount; index += 1) {
    value += Math.floor(Math.random() * 256).toString(16).padStart(2, '0');
  }
  return value;
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

function valueFor(response, key) {
  const item = response?.data?.results?.find(result => result.key === key);
  return item?.value ?? null;
}

module.exports = async ({ params, context, logger }) => {
  if (!context.playerId) throw new Error('AUTHENTICATION_REQUIRED');
  if (!['Ai', 'Private', 'Unranked', 'Ranked'].includes(params.mode)) {
    throw new Error('INVALID_MODE');
  }
  if (!params.buildId || !params.platform) throw new Error('INVALID_CLIENT');

  const cloudSave = new DataApi(context);
  const saved = await cloudSave.getProtectedItems(
    context.projectId, context.playerId, [PROFILE_KEY]);
  const profileItem = saved?.data?.results?.find(result => result.key === PROFILE_KEY);
  const previousProfile = valueFor(saved, PROFILE_KEY);
  const profile = upgradeProfile(previousProfile, context.playerId);
  if (!previousProfile || previousProfile.schemaVersion !== CURRENT_SCHEMA_VERSION) {
    const item = { key: PROFILE_KEY, value: profile };
    if (profileItem?.writeLock) item.writeLock = profileItem.writeLock;
    await cloudSave.setProtectedItem(context.projectId, context.playerId, item);
  }

  const nowSeconds = Math.floor(Date.now() / 1000);
  const ticket = {
    ticketId: randomHex(16),
    playerId: context.playerId,
    mode: params.mode,
    rating: profile.rating,
    buildId: params.buildId,
    platform: params.platform,
    issuedUnixSeconds: nowSeconds,
    expiresUnixSeconds: nowSeconds + 90,
    nonce: randomHex(16),
    consumedMatchId: ''
  };
  await cloudSave.setProtectedItem(
    context.projectId,
    context.playerId,
    { key: ACTIVE_TICKET_KEY, value: ticket });
  logger.info('Duel match ticket created', {
    playerId: context.playerId,
    mode: ticket.mode,
    buildId: ticket.buildId
  });
  return ticket;
};

module.exports.params = {
  mode: { type: 'String', required: true },
  buildId: { type: 'String', required: true },
  platform: { type: 'String', required: true }
};

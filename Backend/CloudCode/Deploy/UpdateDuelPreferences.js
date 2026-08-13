'use strict';

const { DataApi } = require('@unity-services/cloud-save-1.4');

const PROFILE_KEY = 'duel_profile';
const CURRENT_SCHEMA_VERSION = 2;

function createProfile(playerId) {
  return {
    schemaVersion: CURRENT_SCHEMA_VERSION, playerId, displayName: playerId,
    level: 1, experience: 0, rating: 1000, league: 'Silver',
    wins: 0, losses: 0, draws: 0, matchHistory: [], daily: null,
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
    ? upgraded.processedMatchIds : [];
  upgraded.preferences = { ...defaults.preferences, ...(upgraded.preferences || {}) };
  return upgraded;
}

module.exports = async ({ params, context, logger }) => {
  if (!context.playerId) throw new Error('AUTHENTICATION_REQUIRED');
  if (typeof params.inputBindingsJson !== 'string' || params.inputBindingsJson.length > 32768 ||
      typeof params.reducedMotion !== 'boolean' || typeof params.highContrast !== 'boolean' ||
      !Number.isFinite(params.hudScale) || params.hudScale < 0.8 || params.hudScale > 1.5) {
    throw new Error('INVALID_PREFERENCES');
  }

  const cloudSave = new DataApi(context);
  const saved = await cloudSave.getProtectedItems(
    context.projectId, context.playerId, [PROFILE_KEY]);
  const item = saved?.data?.results?.find(result => result.key === PROFILE_KEY);
  const profile = upgradeProfile(item?.value, context.playerId);
  profile.preferences = {
    inputBindingsJson: params.inputBindingsJson,
    reducedMotion: params.reducedMotion,
    highContrast: params.highContrast,
    hudScale: params.hudScale
  };
  const write = { key: PROFILE_KEY, value: profile };
  if (item?.writeLock) write.writeLock = item.writeLock;
  await cloudSave.setProtectedItem(context.projectId, context.playerId, write);
  logger.info('Duel preferences updated', { playerId: context.playerId });
  return profile.preferences;
};

module.exports.params = {
  inputBindingsJson: { type: 'String', required: true },
  reducedMotion: { type: 'Boolean', required: true },
  highContrast: { type: 'Boolean', required: true },
  hudScale: { type: 'Numeric', required: true }
};

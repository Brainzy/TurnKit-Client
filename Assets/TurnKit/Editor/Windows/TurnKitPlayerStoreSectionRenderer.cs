using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitPlayerStoreSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action loadPlayerStoreDefs,
            Action createPlayerStoreDef,
            Action<TurnKitConfig.PlayerStoreDefConfig> deletePlayerStoreDef,
            Action<TurnKitConfig.PlayerStoreDefConfig> drawPlayerStoreDef)
        {
            config.playerStoreDefs ??= new System.Collections.Generic.List<TurnKitConfig.PlayerStoreDefConfig>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Player Store Defs ({config.playerStoreDefs.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                loadPlayerStoreDefs?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Create New", EditorStyles.miniBoldLabel);
            state.NewPlayerStoreKey = EditorGUILayout.TextField("Store Key", state.NewPlayerStoreKey);
            state.NewPlayerStoreValueType = (TurnKitConfig.PlayerStoreValueType)EditorGUILayout.EnumPopup("Value Type", state.NewPlayerStoreValueType);
            state.NewPlayerStoreClientWritable = EditorGUILayout.Toggle("Client Writable", state.NewPlayerStoreClientWritable);
            state.NewPlayerStoreClientReadable = EditorGUILayout.Toggle("Client Readable", state.NewPlayerStoreClientReadable);
            state.NewPlayerStoreCooldownDuration = EditorGUILayout.TextField("Cooldown Duration", state.NewPlayerStoreCooldownDuration);
            EditorGUILayout.LabelField("Format", "ISO-8601 duration, e.g. PT24H", EditorStyles.miniLabel);
            if (state.NewPlayerStoreValueType == TurnKitConfig.PlayerStoreValueType.NUMBER)
            {
                state.NewPlayerStoreNumberMin = EditorGUILayout.TextField("Number Min (Optional)", state.NewPlayerStoreNumberMin);
                state.NewPlayerStoreNumberMax = EditorGUILayout.TextField("Number Max (Optional)", state.NewPlayerStoreNumberMax);
            }
            else
            {
                state.NewPlayerStoreNumberMin = string.Empty;
                state.NewPlayerStoreNumberMax = string.Empty;
            }

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                createPlayerStoreDef?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(6);
            if (config.playerStoreDefs.Count == 0)
            {
                EditorGUILayout.HelpBox("No player store defs loaded.", MessageType.Info);
            }
            else
            {
                foreach (var def in config.playerStoreDefs.ToList())
                {
                    drawPlayerStoreDef?.Invoke(def);
                    GUILayout.Space(4);
                }
            }

            EditorGUILayout.EndVertical();
        }

        internal static void DrawDefCard(TurnKitConfig.PlayerStoreDefConfig def, Action<TurnKitConfig.PlayerStoreDefConfig> deletePlayerStoreDef)
        {
            if (def == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(def.storeKey, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                deletePlayerStoreDef?.Invoke(def);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("Value Type", def.valueType);
            EditorGUILayout.Toggle("Client Writable", def.clientWritable);
            EditorGUILayout.Toggle("Client Readable", def.clientReadable);
            EditorGUILayout.TextField("Cooldown Seconds", def.cooldownSeconds > 0 ? def.cooldownSeconds.ToString() : "(none)");
            if (def.valueType == TurnKitConfig.PlayerStoreValueType.NUMBER)
            {
                EditorGUILayout.TextField("Number Min", def.numberMin.HasValue ? def.numberMin.Value.ToString() : "(none)");
                EditorGUILayout.TextField("Number Max", def.numberMax.HasValue ? def.numberMax.Value.ToString() : "(none)");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }

    internal static class TurnKitLeaderboardSectionRenderer
    {
        internal static void Draw(
            TurnKitConfig config,
            TurnKitEditorWindowState state,
            Action loadLeaderboards,
            Action createLeaderboard,
            Action<string> editLeaderboard,
            Action saveLeaderboardDisplayName,
            Action<TurnKitConfig.LeaderboardConfig> deleteLeaderboard)
        {
            config.leaderboards ??= new System.Collections.Generic.List<TurnKitConfig.LeaderboardConfig>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Leaderboards ({config.leaderboards.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                loadLeaderboards?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Create New", EditorStyles.miniBoldLabel);
            state.NewLeaderboardSlug = EditorGUILayout.TextField("Slug", state.NewLeaderboardSlug);
            state.NewLeaderboardDisplayName = EditorGUILayout.TextField("Display Name", state.NewLeaderboardDisplayName);
            state.NewLeaderboardSortOrder = (TurnKitLeaderboardSortOrder)EditorGUILayout.EnumPopup("Sort Order", state.NewLeaderboardSortOrder);
            state.NewLeaderboardScoreStrategy = (TurnKitLeaderboardScoreStrategy)EditorGUILayout.EnumPopup("Score Strategy", state.NewLeaderboardScoreStrategy);
            state.NewLeaderboardMinScore = EditorGUILayout.TextField("Min Score", state.NewLeaderboardMinScore);
            state.NewLeaderboardMaxScore = EditorGUILayout.TextField("Max Score", state.NewLeaderboardMaxScore);
            state.NewLeaderboardResetFrequency = (TurnKitLeaderboardResetFrequency)EditorGUILayout.EnumPopup("Reset Frequency", state.NewLeaderboardResetFrequency);
            state.NewLeaderboardArchiveOnReset = EditorGUILayout.Toggle("Archive On Reset", state.NewLeaderboardArchiveOnReset);
            state.NewLeaderboardClientSubmitEnabled = EditorGUILayout.Toggle("Client Submit Enabled", state.NewLeaderboardClientSubmitEnabled);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(config.clientKey) || string.IsNullOrEmpty(config.gameKeyId));
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                createLeaderboard?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(6);
            if (config.leaderboards.Count == 0)
            {
                EditorGUILayout.HelpBox("No leaderboards loaded.", MessageType.Info);
            }
            else
            {
                foreach (var leaderboard in config.leaderboards.ToList())
                {
                    DrawCard(state, leaderboard, editLeaderboard, saveLeaderboardDisplayName, deleteLeaderboard);
                    GUILayout.Space(4);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawCard(
            TurnKitEditorWindowState state,
            TurnKitConfig.LeaderboardConfig leaderboard,
            Action<string> editLeaderboard,
            Action saveLeaderboardDisplayName,
            Action<TurnKitConfig.LeaderboardConfig> deleteLeaderboard)
        {
            if (leaderboard == null)
            {
                return;
            }

            string foldoutKey = string.IsNullOrWhiteSpace(leaderboard.slug) ? System.Guid.NewGuid().ToString("N") : leaderboard.slug;
            TurnKitEditorWindowStateController.EnsureLeaderboardFoldout(state, foldoutKey, false);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            state.LeaderboardFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                state.LeaderboardFoldouts[foldoutKey],
                string.IsNullOrWhiteSpace(leaderboard.displayName) ? leaderboard.slug : leaderboard.displayName,
                true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                editLeaderboard?.Invoke(leaderboard.slug);
            }
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                deleteLeaderboard?.Invoke(leaderboard);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Slug: {leaderboard.slug}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Sort: {leaderboard.sortOrder} | Strategy: {leaderboard.scoreStrategy}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Range: {leaderboard.minScore} to {leaderboard.maxScore}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Reset: {leaderboard.resetFrequency} | Archive: {leaderboard.archiveOnReset}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Client Submit: {leaderboard.clientSubmitEnabled}", EditorStyles.miniLabel);
            if (!string.IsNullOrWhiteSpace(leaderboard.nextResetAt))
            {
                EditorGUILayout.LabelField($"Next Reset: {leaderboard.nextResetAt}", EditorStyles.miniLabel);
            }

            if (state.LeaderboardFoldouts[foldoutKey] &&
                string.Equals(state.SelectedLeaderboardSlug, leaderboard.slug, StringComparison.Ordinal))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Update Display Name", EditorStyles.miniBoldLabel);
                state.SelectedLeaderboardDraft.displayName = EditorGUILayout.TextField("Display Name", state.SelectedLeaderboardDraft.displayName);
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(state.SelectedLeaderboardDraft.displayName));
                if (GUILayout.Button("Save Display Name", GUILayout.Width(130)))
                {
                    saveLeaderboardDisplayName?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }
    }
}

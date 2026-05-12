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
            if (def.valueType == TurnKitConfig.PlayerStoreValueType.NUMBER)
            {
                EditorGUILayout.TextField("Number Min", def.numberMin.HasValue ? def.numberMin.Value.ToString() : "(none)");
                EditorGUILayout.TextField("Number Max", def.numberMax.HasValue ? def.numberMax.Value.ToString() : "(none)");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}

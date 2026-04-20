using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TurnKit.Editor
{
    internal static class TurnKitConfigValidator
    {
        private static readonly HashSet<string> ReservedSlugNames = new()
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
            "continue","decimal","default","delegate","do","double","else","enum","event","explicit",
            "extern","false","finally","fixed","float","for","foreach","goto","if","implicit","in","int",
            "interface","internal","is","lock","long","namespace","new","null","object","operator","out",
            "override","params","private","protected","public","readonly","ref","return","sbyte","sealed",
            "short","sizeof","stackalloc","static","string","struct","switch","this","throw","true","try",
            "typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while",
            "add","alias","ascending","async","await","by","descending","dynamic","equals","from","get",
            "global","group","init","into","join","let","managed","nameof","nint","not","notnull","nuint",
            "on","or","orderby","partial","record","remove","required","scoped","select","set","unmanaged",
            "value","var","when","where","with","yield", "spawn"
        };

        public static bool TryValidateConfig(TurnKitConfig config, out List<string> errors)
        {
            errors = new List<string>();

            if (config == null)
            {
                errors.Add("TurnKitConfig is missing.");
                return false;
            }

            config.relayConfigs ??= new List<TurnKitConfig.RelayConfig>();

            var relaySlugs = new HashSet<string>();
            var relayClassNames = new HashSet<string>();

            foreach (var relay in config.relayConfigs)
            {
                if (relay == null)
                {
                    errors.Add("Relay config contains a null entry.");
                    continue;
                }

                NormalizeRelayConfig(relay);

                if (string.IsNullOrWhiteSpace(relay.slug))
                {
                    errors.Add("Relay config slug cannot be empty.");
                }
                else if (!relaySlugs.Add(relay.slug))
                {
                    errors.Add($"Duplicate relay config slug '{relay.slug}'.");
                }

                string className = EnumGenerator.ToConfigClassName(relay.slug);
                if (!relayClassNames.Add(className))
                {
                    errors.Add($"Relay config class name collision. Multiple slugs generate the same class '{className}'.");
                }

                ValidateRelay(relay, errors);
            }

            return errors.Count == 0;
        }

        public static bool TryValidateRelay(TurnKitConfig config, TurnKitConfig.RelayConfig relay, out List<string> errors)
        {
            errors = new List<string>();

            if (config == null)
            {
                errors.Add("TurnKitConfig is missing.");
                return false;
            }

            if (relay == null)
            {
                errors.Add("Relay config is missing.");
                return false;
            }

            config.relayConfigs ??= new List<TurnKitConfig.RelayConfig>();
            NormalizeRelayConfig(relay);

            if (string.IsNullOrWhiteSpace(relay.slug))
            {
                errors.Add("Slug cannot be empty.");
            }

            int duplicateSlugCount = config.relayConfigs.Count(item => item != null && string.Equals(item.slug, relay.slug));
            if (!string.IsNullOrWhiteSpace(relay.slug) && duplicateSlugCount > 1)
            {
                errors.Add($"Duplicate relay config slug '{relay.slug}'.");
            }

            string className = EnumGenerator.ToConfigClassName(relay.slug);
            int classCollisionCount = config.relayConfigs
                .Where(item => item != null)
                .Count(item => EnumGenerator.ToConfigClassName(item.slug) == className);
            if (classCollisionCount > 1)
            {
                errors.Add($"Relay config class name collision. Multiple slugs generate the same class '{className}'.");
            }

            ValidateRelay(relay, errors);
            return errors.Count == 0;
        }

        public static bool TryGetSlugNameError(string value, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Name cannot be empty.";
                return false;
            }

            if (value.Length > 64)
            {
                error = "Name must be 64 characters or less.";
                return false;
            }

            if (!Regex.IsMatch(value, "^[a-z][a-z0-9_]*$"))
            {
                error = "Use lowercase letters, numbers, and underscores only, and start with a letter.";
                return false;
            }

            if (value.Contains("__"))
            {
                error = "Double underscores are not allowed.";
                return false;
            }

            if (ReservedSlugNames.Contains(value))
            {
                error = "This name is reserved in C# and cannot be used.";
                return false;
            }

            return true;
        }

        public static bool TryGetTrackedStatNameError(string value, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Name cannot be empty.";
                return false;
            }

            if (value.Length > 64)
            {
                error = "Name must be 64 characters or less.";
                return false;
            }

            if (!Regex.IsMatch(value, "^[_a-zA-Z][_a-zA-Z0-9]*$"))
            {
                error = "Use a C# identifier: letters, numbers, and underscores only, starting with a letter or underscore.";
                return false;
            }

            return true;
        }

        public static string BuildErrorSummary(IEnumerable<string> errors, int maxLines = 6)
        {
            var items = errors?.Where(error => !string.IsNullOrWhiteSpace(error)).Take(maxLines).ToList() ?? new List<string>();
            if (items.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"- {item}");
            }

            return sb.ToString().TrimEnd();
        }

        public static void LogErrors(IEnumerable<string> errors)
        {
            foreach (var error in errors ?? Enumerable.Empty<string>())
            {
                Debug.LogError($"[TurnKit] {error}");
            }
        }

        private static void ValidateRelay(TurnKitConfig.RelayConfig relay, List<string> errors)
        {
            if (relay.votingEnabled)
            {
                if (relay.votesRequired > relay.maxPlayers)
                {
                    errors.Add($"{relay.slug}: Votes required ({relay.votesRequired}) exceeds max players ({relay.maxPlayers}).");
                }

                if (relay.votesToFail > relay.maxPlayers)
                {
                    errors.Add($"{relay.slug}: Votes to fail ({relay.votesToFail}) exceeds max players ({relay.maxPlayers}).");
                }

                if (!ValidateVotingConfiguration(relay, out string votingError))
                {
                    errors.Add(votingError);
                }
            }

            var listNames = new HashSet<string>();
            foreach (var list in relay.lists)
            {
                if (list == null)
                {
                    errors.Add($"{relay.slug}: List contains a null entry.");
                    continue;
                }

                list.ownerSlots ??= new List<TurnKitConfig.PlayerSlot>();
                list.visibleToSlots ??= new List<TurnKitConfig.PlayerSlot>();
                list.ownerSlots.RemoveAll(slot => (int)slot < 1 || (int)slot > relay.maxPlayers);
                list.visibleToSlots.RemoveAll(slot => (int)slot < 1 || (int)slot > relay.maxPlayers);

                if (!TryGetSlugNameError(list.name, out string listNameError))
                {
                    errors.Add($"{relay.slug}: Invalid list name '{list.name}'. {listNameError}");
                }
                else if (!listNames.Add(list.name))
                {
                    errors.Add($"{relay.slug}: Duplicate list name '{list.name}'.");
                }

                if (!string.IsNullOrWhiteSpace(list.tag) && !TryGetSlugNameError(list.tag, out string tagError))
                {
                    errors.Add($"{relay.slug}: Invalid list tag '{list.tag}'. {tagError}");
                }
            }

            var statNames = new HashSet<string>();
            var generatedStatMemberNames = new HashSet<string>();
            foreach (var stat in relay.trackedStats)
            {
                if (stat == null)
                {
                    errors.Add($"{relay.slug}: Tracked stat contains a null entry.");
                    continue;
                }

                stat.initialList ??= new List<string>();
                stat.syncTo ??= new List<TurnKitConfig.TrackedStatSyncTargetConfig>();

                if (!TryGetTrackedStatNameError(stat.name, out string statNameError))
                {
                    errors.Add($"{relay.slug}: Invalid tracked stat name '{stat.name}'. {statNameError}");
                }
                else
                {
                    if (!statNames.Add(stat.name))
                    {
                        errors.Add($"{relay.slug}: Duplicate tracked stat name '{stat.name}'.");
                    }

                    string memberName = EnumGenerator.ToEnumMemberName(stat.name);
                    if (!generatedStatMemberNames.Add(memberName))
                    {
                        errors.Add($"{relay.slug}: Tracked stat name collision. Multiple names generate '{memberName}'.");
                    }
                }

                foreach (var target in stat.syncTo)
                {
                    if (target == null)
                    {
                        errors.Add($"{relay.slug}: Tracked stat '{stat.name}' contains a null sync target.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(target.destinationId))
                    {
                        errors.Add($"{relay.slug}: Tracked stat '{stat.name}' has a sync target with an empty destination id.");
                    }

                    if (target.destinationType == TurnKitConfig.TrackedStatSyncDestinationType.LEADERBOARD &&
                        (stat.dataType != TurnKitConfig.TrackedStatDataType.DOUBLE || stat.scope != TurnKitConfig.TrackedStatScope.PER_PLAYER))
                    {
                        errors.Add($"{relay.slug}: Tracked stat '{stat.name}' must be PER_PLAYER DOUBLE to sync to a leaderboard.");
                    }
                }
            }
        }

        private static void NormalizeRelayConfig(TurnKitConfig.RelayConfig relay)
        {
            relay.lists ??= new List<TurnKitConfig.RelayListConfig>();
            relay.trackedStats ??= new List<TurnKitConfig.TrackedStatConfig>();
        }

        private static bool ValidateVotingConfiguration(TurnKitConfig.RelayConfig relay, out string error)
        {
            error = null;

            if (relay == null || !relay.votingEnabled)
            {
                return true;
            }

            if (relay.votingMode == TurnKitConfig.VotingMode.ASYNC &&
                relay.failAction != TurnKitConfig.FailAction.END_GAME)
            {
                error = $"{relay.slug}: ASYNC voting requires END_GAME fail action.";
                return false;
            }

            return true;
        }
    }
}

// DynamicOpenVR - Unity scripts to allow dynamic creation of OpenVR actions at runtime.
// Copyright � 2019 Nicolas Gnyra

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/.

using DynamicOpenVR.IO;
using DynamicOpenVR.Manifest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicOpenVR.DefaultBindings;
using UnityEngine;
using Valve.VR;
using Debug = UnityEngine.Debug;
using Input = DynamicOpenVR.IO.Input;

namespace DynamicOpenVR
{
	public class OpenVRActionManager : MonoBehaviour
	{
        public static readonly string ActionManifestPath = Path.Combine(Environment.CurrentDirectory, "DynamicOpenVR", "action_manifest.json");

        public static bool IsRunning => OpenVRWrapper.IsRunning;

        private static OpenVRActionManager instance;

        public static OpenVRActionManager Instance
		{
			get
			{
                if (!OpenVRWrapper.IsRunning)
                {
                    throw new InvalidOperationException("OpenVR is not running");
                }

				if (!instance)
				{
					GameObject go = new GameObject(nameof(OpenVRActionManager));
					DontDestroyOnLoad(go);
					instance = go.AddComponent<OpenVRActionManager>();
                }

				return instance;
			}
        }

        private Dictionary<string, OVRAction> actions = new Dictionary<string, OVRAction>();
        private ulong[] actionSetHandles;
        private bool instantiated = false;

        private void Start()
        {
            instantiated = true;
            
            List<ManifestDefaultBinding> defaultBindingFiles = CombineAndWriteBindings();
            CombineAndWriteManifest(defaultBindingFiles);
                
            OpenVRWrapper.SetActionManifestPath(ActionManifestPath);

            List<string> actionSetNames = actions.Values.Select(action => action.GetActionSetName()).Distinct().ToList();
            actionSetHandles = new ulong[actionSetNames.Count];

            Console.WriteLine(string.Join(", ", actionSetNames));

            for (int i = 0; i < actionSetNames.Count; i++)
            {
                actionSetHandles[i] = OpenVRWrapper.GetActionSetHandle(actionSetNames[i]);
            }

            Console.WriteLine(string.Join(", ", actionSetHandles));

            foreach (var action in actions.Values)
            {
                action.UpdateHandle();
            }
        }

        public void Update()
        {
            if (actionSetHandles != null)
            {
                OpenVRWrapper.UpdateActionState(actionSetHandles);
            }
        }

        public T RegisterAction<T>(T action) where T : OVRAction
        {
            if (instantiated)
            {
                throw new InvalidOperationException("Cannot register new actions once game is running");
            }

            if (actions.ContainsKey(action.Name))
            {
                throw new InvalidOperationException($"An action with the name '{action.Name}' was already registered.");
            }

            actions.Add(action.Name, action);

            return action;
        }

        private void CombineAndWriteManifest(List<ManifestDefaultBinding> defaultBindings)
		{
            string[] actionFiles = Directory.GetFiles("DynamicOpenVR/Actions");
            var actionManifests = new List<ActionManifest>();

            foreach (string actionFile in actionFiles)
            {
                try
                {
                    using (var reader = new StreamReader(actionFile))
                    {
                        actionManifests.Add(JsonConvert.DeserializeObject<ActionManifest>(reader.ReadToEnd()));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"An error of type {ex.GetType().FullName} occured when trying to parse {actionFile}: {ex.Message}");
                }
            }

            using (var writer = new StreamWriter(ActionManifestPath))
            {
                var manifest = new ActionManifest()
                {
                    Actions = actionManifests.SelectMany(m => m.Actions).ToList(),
                    ActionSets = actionManifests.SelectMany(m => m.ActionSets).ToList(),
                    DefaultBindings = defaultBindings,
                    Localization = CombineLocalizations(actionManifests)
                };

                writer.WriteLine(JsonConvert.SerializeObject(manifest, Formatting.Indented));
            }
		}

        private List<ManifestDefaultBinding> CombineAndWriteBindings()
        {
            string[] bindingFiles = Directory.GetFiles("DynamicOpenVR/Bindings");
            var defaultBindings = new List<DefaultBinding>();

            foreach (string bindingFile in bindingFiles)
            {
                try
                {
                    using (var reader = new StreamReader(bindingFile))
                    {
                        defaultBindings.Add(JsonConvert.DeserializeObject<DefaultBinding>(reader.ReadToEnd()));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"An error of type {ex.GetType().FullName} occured when trying to parse {bindingFile}: {ex.Message}");
                }
            }

            var combinedBindings = new List<ManifestDefaultBinding>();

            foreach (string controllerType in defaultBindings.Select(b => b.ControllerType).Distinct())
            {
                var defaultBinding = new DefaultBinding
                {
                    Name = "Default Beat Saber Bindings",
                    Description = "Action bindings for Beat Saber.",
                    ControllerType = controllerType,
                    Category = "steamvr_input",
                    Bindings = MergeBindings(defaultBindings.Where(b => b.ControllerType == controllerType))
                };

                string fileName = $"default_bindings_{defaultBinding.ControllerType}.json";
                combinedBindings.Add(new ManifestDefaultBinding { ControllerType = controllerType, BindingUrl = fileName });

                using (StreamWriter writer = new StreamWriter(Path.Combine("DynamicOpenVR", fileName)))
                {
                    writer.WriteLine(JsonConvert.SerializeObject(defaultBinding, Formatting.Indented));
                }
            }

            return combinedBindings;
        }

        private Dictionary<string, BindingCollection> MergeBindings(IEnumerable<DefaultBinding> bindingSets)
        {
            var final = new Dictionary<string, BindingCollection>();

            foreach (var bindingSet in bindingSets)
            {
                foreach (KeyValuePair<string, BindingCollection> kvp in bindingSet.Bindings)
                {
                    string actionSetName = kvp.Key;
                    BindingCollection bindings = kvp.Value;

                    if (!final.ContainsKey(actionSetName))
                    {
                        final.Add(actionSetName, new BindingCollection());
                    }
                    
                    final[actionSetName].Chords.AddRange(bindings.Chords);
                    final[actionSetName].Haptics.AddRange(bindings.Haptics);
                    final[actionSetName].Poses.AddRange(bindings.Poses);
                    final[actionSetName].Skeleton.AddRange(bindings.Skeleton);
                    final[actionSetName].Sources.AddRange(bindings.Sources);
                }
            }

            return final;
        }

        private List<Dictionary<string, string>> CombineLocalizations(IEnumerable<ActionManifest> manifests)
        {
            var combinedLocalizations = new Dictionary<string, Dictionary<string, string>>();

            foreach (var manifest in manifests)
            {
                foreach (var language in manifest.Localization)
                {
                    if (!language.ContainsKey("language_tag"))
                    {
                        continue;
                    }

                    if (!combinedLocalizations.ContainsKey(language["language_tag"]))
                    {
                        combinedLocalizations.Add(language["language_tag"], new Dictionary<string, string>() { {"language_tag", language["language_tag"] } });
                    }

                    foreach (var kvp in language.Where(kvp => kvp.Key != "language_tag"))
                    {
                        if (combinedLocalizations.ContainsKey(kvp.Key))
                        {
                            Debug.LogWarning($"Duplicate entry {kvp.Key}");
                        }
                        else
                        {
                            combinedLocalizations[language["language_tag"]].Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }

            return combinedLocalizations.Values.ToList();
        }
	}
}

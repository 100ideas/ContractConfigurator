﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP;
using Contracts;

namespace ContractConfigurator
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ContractConfigurator : MonoBehaviour
    {
        static bool reloading = false;
        static bool loaded = false;
        static bool contractTypesAdjusted = false;

        private bool showGUI = false;
        private Rect windowPos = new Rect(320f, 100f, 240f, 40f);

        private int totalContracts = 0;
        private int successContracts = 0;

        private bool contractsAppVisible = false;

        public static EventData<Contract, ContractParameter> OnParameterChange = new EventData<Contract, ContractParameter>("OnParameterChange");

        void Start()
        {
            DontDestroyOnLoad(this);
            OnParameterChange.Add(new EventData<Contract, ContractParameter>.OnEvent(ParameterChange));
        }

        void Destroy()
        {
            OnParameterChange.Remove(new EventData<Contract, ContractParameter>.OnEvent(ParameterChange));
        }

        void Update()
        {
            // Load all the contract configurator configuration
            if (HighLogic.LoadedScene == GameScenes.MAINMENU && !loaded)
            {
                LoggingUtil.LoadDebuggingConfig();
                RegisterParameterFactories();
                RegisterBehaviourFactories();
                RegisterContractRequirements();
                LoadContractConfig();
                loaded = true;
            }
            // Try to disable the contract types
            else if ((HighLogic.LoadedScene == GameScenes.SPACECENTER) && !contractTypesAdjusted)
            {
                if (AdjustContractTypes())
                {
                    contractTypesAdjusted = true;
                }
            }

            // Alt-F9 shows the contract configurator window
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.F10))
            {
                showGUI = !showGUI;
            }

            // Check if the ContractsApp has just become visible, and fire off an update event
            if (!contractsAppVisible &&
                ContractsApp.Instance != null &&
                ContractsApp.Instance.appLauncherButton != null &&
                ContractsApp.Instance.cascadingList.cascadingList != null &&
                ContractsApp.Instance.cascadingList.cascadingList.gameObject.activeInHierarchy)
            {
                contractsAppVisible = true;

                // Fire off an event for each contract
                for (int i = 0; i < ContractSystem.Instance.Contracts.Count; i++)
                {
                    Contract contract = ContractSystem.Instance.Contracts[i];
                    if (contract.ContractState == Contract.State.Active && contract.GetType() == typeof(ConfiguredContract))
                    {
                        GameEvents.Contract.onParameterChange.Fire(contract, contract.GetParameter(0));
                    }
                }
            }
        }

        private void ParameterChange(Contract c, ContractParameter p)
        {
            // ContractsApp is visible
            if (ContractsApp.Instance != null &&
                ContractsApp.Instance.appLauncherButton != null &&
                ContractsApp.Instance.cascadingList.cascadingList != null &&
                ContractsApp.Instance.cascadingList.cascadingList.gameObject.activeInHierarchy)
            {
                // Just let the event flow through
                GameEvents.Contract.onParameterChange.Fire(c, p);
                contractsAppVisible = true;
            }
            // Not visible
            else
            {
                contractsAppVisible = false;
            }
        }

        public void OnGUI()
        {
            if (showGUI && (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.MAINMENU))
            {
                windowPos = GUILayout.Window(
                    GetType().FullName.GetHashCode(),
                    windowPos,
                    WindowGUI,
                    "Contract Configurator " + GetType().Assembly.GetName().Version.ToString(),
                    GUILayout.Width(200),
                    GUILayout.Height(20));
            }
        }

        protected void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            if (GUILayout.Button("Reload Contracts"))
                StartCoroutine(ReloadContractTypes());
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private IEnumerator<ContractType> ReloadContractTypes()
        {
            reloading = true;

            // Infrom the player of the reload process
            ScreenMessages.PostScreenMessage("Reloading game database...", 2,
                ScreenMessageStyle.UPPER_CENTER);
            yield return null;

            GameDatabase.Instance.Recompile = true;
            GameDatabase.Instance.StartLoad();

            // Wait for the reload
            while (!GameDatabase.Instance.IsReady())
                yield return null;

            // Attempt to find module manager and do their reload
            var allMM = from loadedAssemblies in AssemblyLoader.loadedAssemblies
                           let assembly = loadedAssemblies.assembly
                           where assembly.GetName().Name.StartsWith("ModuleManager")
                           orderby assembly.GetName().Version descending, loadedAssemblies.path ascending
                           select loadedAssemblies;

            // Reload module manager
            if (allMM.Count() > 0)
            {
                ScreenMessages.PostScreenMessage("Reloading module manager...", 2,
                    ScreenMessageStyle.UPPER_CENTER);

                Assembly mmAssembly = allMM.First().assembly;
                LoggingUtil.LogVerbose(this, "Reloading config using ModuleManager: " + mmAssembly.FullName);

                // Get the module manager object
                Type mmPatchType = mmAssembly.GetType("ModuleManager.MMPatchLoader");
                UnityEngine.Object mmPatchLoader = FindObjectOfType(mmPatchType);

                // Get the methods
                MethodInfo methodStartLoad = mmPatchType.GetMethod("StartLoad", Type.EmptyTypes);
                MethodInfo methodIsReady = mmPatchType.GetMethod("IsReady");

                // Start the load
                methodStartLoad.Invoke(mmPatchLoader, null);

                // Wait for it to complete
                while (!(bool)methodIsReady.Invoke(mmPatchLoader, null))
                {
                    yield return null;
                }
            }

            ScreenMessages.PostScreenMessage("Reloading contract types...", 1.5f,
                ScreenMessageStyle.UPPER_CENTER);

            // Reload contract configurator
            ClearContractConfig();
            LoadContractConfig();
            AdjustContractTypes();

            // We're done!
            ScreenMessages.PostScreenMessage("Loaded " + successContracts + " out of " + totalContracts
                + " contracts successfully.", 5, ScreenMessageStyle.UPPER_CENTER);

            reloading = false;
        }


        /*
         * Registers all the out of the box ParameterFactory classes.
         */
        void RegisterParameterFactories()
        {
            LoggingUtil.LogDebug(this.GetType(), "Start Registering ParameterFactories");

            // Register each type with the parameter factory
            foreach (Type subclass in GetAllTypes<ParameterFactory>())
            {
                string name = subclass.Name;
                if (name.EndsWith("Factory"))
                {
                    name = name.Remove(name.Length - 7, 7);
                }

                ParameterFactory.Register(subclass, name);
            }

            LoggingUtil.LogInfo(this.GetType(), "Finished Registering ParameterFactories");
        }

        /*
         * Registers all the out of the box BehaviourFactory classes.
         */
        void RegisterBehaviourFactories()
        {
            LoggingUtil.LogDebug(this.GetType(), "Start Registering BehaviourFactories");

            // Register each type with the behaviour factory
            foreach (Type subclass in GetAllTypes<BehaviourFactory>())
            {
                string name = subclass.Name;
                if (name.EndsWith("Factory"))
                {
                    name = name.Remove(name.Length - 7, 7);
                }
                BehaviourFactory.Register(subclass, name);
            }

            LoggingUtil.LogInfo(this.GetType(), "Finished Registering BehaviourFactories");
        }

        /*
         * Registers all the out of the box ContractRequirement classes.
         */
        void RegisterContractRequirements()
        {
            LoggingUtil.LogDebug(this.GetType(), "Start Registering ContractRequirements");

            // Register each type with the parameter factory
            foreach (Type subclass in GetAllTypes<ContractRequirement>())
            {
                string name = subclass.Name;
                if (name.EndsWith("Requirement"))
                {
                    name = name.Remove(name.Length - 11, 11);
                }
                ContractRequirement.Register(subclass, name);
            }

            LoggingUtil.LogInfo(this.GetType(), "Finished Registering ContractRequirements");
        }

        /*
         * Clears the contract configuration.
         */
        void ClearContractConfig()
        {
            ContractType.contractTypes.Clear();
            totalContracts = successContracts = 0;
        }

        /*
         * Loads all the contact configuration nodes and creates ContractType objects.
         */
        void LoadContractConfig()
        {
            LoggingUtil.LogDebug(this.GetType(), "Loading CONTRACT_TYPE nodes.");
            ConfigNode[] contractConfigs = GameDatabase.Instance.GetConfigNodes("CONTRACT_TYPE");

            // First pass - create all the ContractType objects
            foreach (ConfigNode contractConfig in contractConfigs)
            {
                totalContracts++;
                LoggingUtil.LogVerbose(this.GetType(), "Pre-load for node: '" + contractConfig.GetValue("name") + "'");
                // Create the initial contract type
                try
                {
                    ContractType contractType = new ContractType(contractConfig.GetValue("name"));
                }
                catch (ArgumentException)
                {
                    LoggingUtil.LogError(this.GetType(), "Couldn't load CONTRACT_TYPE '" + contractConfig.GetValue("name") + "' due to a duplicate name.");

                    // BUG: The same contract will get loaded twice, but just decrement the success counter so one shows as failed
                    successContracts--;
                }
            }

            // Second pass - do the actual loading of details
            foreach (ConfigNode contractConfig in contractConfigs)
            {
                // Fetch the contractType
                string name = contractConfig.GetValue("name");
                ContractType contractType = ContractType.contractTypes[name];
                bool success = false;
                if (contractType != null)
                {
                    LoggingUtil.LogDebug(this.GetType(), "Loading CONTRACT_TYPE: '" + name + "'");
                    // Perform the load
                    try
                    {
                        if (contractType.Load(contractConfig))
                        {
                            successContracts++;
                            success = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Exception wrapper = new Exception("Error loading CONTRACT_TYPE '" + name + "'", e);
                        Debug.LogException(wrapper);
                    }
                    finally
                    {
                        if (!success)
                        {
                            ContractType.contractTypes.Remove(name);
                        }
                    }
                }
            }

            LoggingUtil.LogInfo(this.GetType(), "Loaded " + successContracts + " out of " + totalContracts + " CONTRACT_TYPE nodes.");

            if (!reloading && LoggingUtil.logLevel == LoggingUtil.LogLevel.DEBUG || LoggingUtil.logLevel == LoggingUtil.LogLevel.VERBOSE)
            {
                ScreenMessages.PostScreenMessage("Contract Configurator: Loaded " + successContracts + " out of " + totalContracts
                    + " contracts successfully.", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        /*
         * Performs adjustments to the contract type list.  Specifically, disables contract types
         * as per configuration files and adds addtional ConfiguredContract instances based on the
         * number on contract types.
         */
        bool AdjustContractTypes()
        {
            // Don't do anything if the contract system has not yet loaded
            if (ContractSystem.ContractTypes == null)
            {
                return false;
            }

            LoggingUtil.LogDebug(this.GetType(), "Loading CONTRACT_CONFIGURATOR nodes.");
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("CONTRACT_CONFIGURATOR");

            // Build a unique list of contract types to disable, in case multiple mods try to
            // disable the same ones.
            Dictionary<string, Type> contractsToDisable = new Dictionary<string, Type>();
            foreach (ConfigNode node in nodes)
            {
                foreach (string contractType in node.GetValues("disabledContractType"))
                {
                    // No type for now
                    contractsToDisable[contractType] = null;
                }
            }

            // Map the string to a type
            foreach (Type subclass in GetAllTypes<Contract>())
            {
                string name = subclass.Name;
                if (contractsToDisable.ContainsKey(name))
                {
                    contractsToDisable[name] = subclass;
                }
            }

            // Start disabling!
            int disabledCounter = 0;
            foreach (KeyValuePair<string, Type> p in contractsToDisable)
            {
                // Didn't find a type
                if (p.Value == null)
                {
                    LoggingUtil.LogWarning(this.GetType(), "Couldn't find ContractType '" + p.Key + "' to disable.");
                }
                else
                {
                    LoggingUtil.LogDebug(this.GetType(), "Disabling ContractType: " + p.Value.FullName + " (" + p.Value.Module + ")");
                    ContractSystem.ContractTypes.Remove(p.Value);
                    disabledCounter++;
                }
            }

            LoggingUtil.LogInfo(this.GetType(), "Disabled " + disabledCounter + " ContractTypes.");

            // Now add the ConfiguredContract type
            int count = (int)(ContractType.contractTypes.Count / 4.0 + 0.5);
            for (int i = 0; i < count; i++)
            {
                ContractSystem.ContractTypes.Add(typeof(ConfiguredContract));
            }

            LoggingUtil.LogInfo(this.GetType(), "Finished Adjusting ContractTypes");

            return true;
        }

        public static List<Type> GetAllTypes<T>()
        {
            // Get everything that extends ParameterFactory
            List<Type> allTypes = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in from type in assembly.GetTypes() where type.IsSubclassOf(typeof(T)) select type)
                    {
                        allTypes.Add(t);
                    }
                }
                catch (Exception e)
                {
                    LoggingUtil.LogWarning(typeof(ContractConfigurator), "Error loading types from assembly " + assembly.FullName + ": " + e.Message);
                }
            }

            return allTypes;
        }

        /// <summary>
        /// Verify the loaded assembly meets a minimum version number.
        /// </summary>
        /// <param name="name">Assembly name</param>
        /// <param name="version">Minium version</param>
        /// <returns>True if the version check was successful.  If not, logs and error and returns false.</returns>
        public static bool VerifyAssemblyVersion(string name, string version)
        {
            // Logic courtesy of DMagic
            var assembly = AssemblyLoader.loadedAssemblies.SingleOrDefault(a => a.assembly.GetName().Name == name);
            if (assembly != null)
            {
                var ainfoV = Attribute.GetCustomAttribute(assembly.assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                if (ainfoV != null)
                {
                    Version expected = ParseVersion(version);
                    Version received = ParseVersion(ainfoV.InformationalVersion);
                    if (expected >= received)
                    {
                        LoggingUtil.LogVerbose(typeof(ContractConfigurator), "Version check for '" + name + "' passed.  Minimum required is " + version + ", version found was " + ainfoV.InformationalVersion);
                        return true;
                    }
                    else
                    {
                        LoggingUtil.LogError(typeof(ContractConfigurator), "Version check for '" + name + "' failed!  Minimum required is " + version + ", version found was " + ainfoV.InformationalVersion);
                        return false;
                    }
                }
                else
                {
                    LoggingUtil.LogError(typeof(ContractConfigurator), "Couldn't determine version of '" + name + "' assembly!");
                    return false;
                }
            }
            else
            {
                LoggingUtil.LogError(typeof(ContractConfigurator), "Couldn't find assembly for '" + name + "'!");
                return false;
            }
        }

        private static Version ParseVersion(string version)
        {
            Match m = Regex.Match(version, @"^[vV]?(\d)+(.(\d)+(.(\d)+(.(\d)+)?)?)?");
            int major = m.Groups[1].Value.Equals("") ? 0 : Convert.ToInt32(m.Groups[1].Value);
            int minor = m.Groups[3].Value.Equals("") ? 0 : Convert.ToInt32(m.Groups[3].Value);
            int build = m.Groups[5].Value.Equals("") ? 0 : Convert.ToInt32(m.Groups[5].Value);
            int revision = m.Groups[7].Value.Equals("") ? 0 : Convert.ToInt32(m.Groups[7].Value);

            return new Version(major, minor, build, revision);
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;

namespace ContractConfigurator
{
    /*
     * Class for generating ContractBehaviour objects.
     */
    public abstract class BehaviourFactory
    {
        private static Dictionary<string, Type> factories = new Dictionary<string, Type>();

        protected virtual ContractType contractType { get; set; }

        /*
         * Loads the BehaviourFactory from the given ConfigNode.
         */
        public virtual bool Load(ConfigNode configNode)
        {
            return true;
        }

        /*
         * Method for generating ContractBehaviour objects.  Each time it is called it should
         * generate a new object for the given contract.  The object does not need to be
         * added to the contract, as that gets done elsewhere (the contract is simply passed
         * to be used in behaviour generation logic).
         */
        public abstract ContractBehaviour Generate(ConfiguredContract contract);

        /*
         * Generates all the ContractBehaviour objects required for the array of ConfigNodes, and
         * adds them to the host object.
         */
        public static void GenerateBehaviours(ConfiguredContract contract, List<BehaviourFactory> behaviourNodes)
        {
            foreach (BehaviourFactory behaviourFactory in behaviourNodes)
            {
                ContractBehaviour behaviour = behaviourFactory.Generate(contract);
                if (behaviour == null)
                {
                    throw new Exception(behaviourFactory.GetType().FullName + ".Generate() returned a null ContractBehaviour!");
                }

                // Add ContractBehaviour to the host
                contract.AddBehaviour(behaviour);
            }
        }

        /*
         * Adds a new BehaviourFactory to handle Behaviour nodes with the given type.
         */
        public static void Register(Type factory, string type)
        {
            Debug.Log("ContractConfigurator: Registered behaviour factory class " +
                factory.FullName + " for handling Behaviour nodes with type = " + type + ".");
            factories.Add(type, factory);
        }

        /*
         * Generates a new BehaviourFactory from the given ConfigNode.
         */
        public static BehaviourFactory GenerateBehaviourFactory(ConfigNode behaviourConfig, ContractType contractType)
        {
            // Get the type
            string type = behaviourConfig.GetValue("type");
            if (!factories.ContainsKey(type))
            {
                Debug.LogError("ContractConfigurator: No BehaviourFactory has been registered for type '" + type + "'.");
                return null;
            }

            // Create an instance of the factory
            BehaviourFactory behaviourFactory = (BehaviourFactory)Activator.CreateInstance(factories[type]);

            // Set attributes
            behaviourFactory.contractType = contractType;

            // Load config
            if (!behaviourFactory.Load(behaviourConfig))
            {
                return null;
            }

            return behaviourFactory;
        }

        public string ErrorPrefix(ConfigNode configNode)
        {
            return "Behaviour '" + configNode.GetValue("name") + "' of type '" + configNode.GetValue("type") + "'";
        }
    }
}

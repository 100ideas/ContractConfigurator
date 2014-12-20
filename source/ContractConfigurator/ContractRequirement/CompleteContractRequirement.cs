﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using KSPAchievements;
using Contracts;

namespace ContractConfigurator
{
    /*
     * ContractRequirement to provide requirement for player having completed other contracts.
     */
    public class CompleteContractRequirement : ContractRequirement
    {
        protected string ccType { get; set; }
        protected Type contractClass { get; set; }
        protected uint minCount { get; set; }
        protected uint maxCount { get; set; }
        protected double cooldown { get; set; }

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            // Get type
            valid &= ConfigNodeUtil.ValidateMandatoryField(configNode, "contractType", this);
            if (valid)
            {
                string contractType = configNode.GetValue("contractType");

                if (ContractType.contractTypes.Keys.Contains(contractType))
                {
                    ccType = contractType;
                }
                else
                {
                    ccType = null;

                    // Search for the correct type
                    var classes =
                        from assembly in AppDomain.CurrentDomain.GetAssemblies()
                        from type in assembly.GetTypes()
                        where type.IsSubclassOf(typeof(Contract)) && type.Name.Equals(contractType)
                        select type;

                    if (classes.Count() < 1)
                    {
                        valid = false;
                        LoggingUtil.LogError(this.GetType(), "contractType '" + contractType +
                            "' must either be a Contract sub-class or ContractConfigurator contract type");
                    }
                    else
                    {
                        contractClass = classes.First();
                    }
                }
            }

            // Get minCount
            if (!configNode.HasValue("minCount"))
            {
                minCount = 1;
            }
            else
            {
                try
                {
                    minCount = Convert.ToUInt32(configNode.GetValue("minCount"));
                }
                catch (Exception e)
                {
                    valid = false;
                    LoggingUtil.LogError(this.GetType(), ErrorPrefix(configNode) +
                        ": minCount: " + e.Message);
                }
            }

            // Get maxCount
            if (!configNode.HasValue("maxCount"))
            {
                maxCount = UInt32.MaxValue;
            }
            else
            {
                try
                {
                    maxCount = Convert.ToUInt32(configNode.GetValue("maxCount"));
                }
                catch (Exception e)
                {
                    valid = false;
                    LoggingUtil.LogError(this.GetType(), ErrorPrefix(configNode) +
                        ": maxCount: " + e.Message);
                }
            }

            // Get cooldown
            cooldown = configNode.HasValue("cooldownDuration") ? DurationUtil.ParseDuration(configNode, "cooldownDuration") : 0.0;

            return valid;
        }

        public override bool RequirementMet(ConfiguredContract contract)
        {
            // Get the count of finished contracts
            int finished = 0;
            double lastFinished = 0.0;

            // Finished contracts - Contract Configurator style
            if (ccType != null)
            {
                IEnumerable<ConfiguredContract> completedContract = ContractSystem.Instance.GetCompletedContracts<ConfiguredContract>().Where(c => c.contractType.name.Equals(ccType));
                finished = completedContract.Count();
                if (finished > 0)
                {
                    // TODO - this isn't working out
                    lastFinished = completedContract.OrderByDescending<ConfiguredContract, double>(c => c.DateFinished).First().DateFinished;
                }
            }
            // Finished contracts - stock style
            else
            {
                // Call the GetCompletedContracts with our type, and get the count
                Contract[] completedContract = (Contract[])typeof(ContractSystem).GetMethod("GetCompletedContracts").MakeGenericMethod(contractClass).Invoke(ContractSystem.Instance, null);
                finished = completedContract.Count();
                if (finished > 0)
                {
                    lastFinished = completedContract.OrderByDescending<Contract, double>(c => c.DateFinished).First().DateFinished;
                }
            }

            // Check cooldown
            if (cooldown > 0.0 && finished > 0 && lastFinished + cooldown > Planetarium.GetUniversalTime())
            {
                return false;
            }

            // Return based on the min/max counts configured
            return (finished >= minCount) && (finished <= maxCount);
        }
    }
}

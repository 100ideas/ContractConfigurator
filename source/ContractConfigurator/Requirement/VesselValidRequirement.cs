﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using KSPAchievements;
using Contracts;
using ContractConfigurator.ExpressionParser;

namespace ContractConfigurator
{
    /// <summary>
    /// ContractRequirement to check if a VesselIdentifier is assigned to a valid vessel.
    /// </summary>
    public class ValidVesselRequirement : ContractRequirement
    {
        protected string vessel;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            // Get expression
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "vessel", x => vessel = x, this);

            return valid;
        }

        public override void SaveToPersistence(ConfigNode configNode)
        {
            base.SaveToPersistence(configNode);

            configNode.AddValue("vessel", vessel);
        }

        public override void LoadFromPersistence(ConfigNode configNode)
        {
            base.LoadFromPersistence(configNode);

            vessel = ConfigNodeUtil.ParseValue<string>(configNode, "vessel");
        }

        public override bool RequirementMet(ConfiguredContract contract)
        {
            return ContractVesselTracker.Instance != null && ContractVesselTracker.Instance.GetAssociatedVessel(vessel) != null;
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using ContractConfigurator.Parameters;

namespace ContractConfigurator
{
    /*
     * ParameterFactory wrapper for ReachSituation ContractParameter.
     */
    public class ReachSituationFactory : ParameterFactory
    {
        protected Vessel.Situations situation { get; set; }
        protected string title { get; set; }

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            // Get situation
            if (!configNode.HasValue("situation"))
            {
                valid = false;
                Debug.LogError("ContractConfigurator: " + ErrorPrefix(configNode) +
                    ": missing required value 'situation'.");
            }
            try
            {
                string situationStr = configNode.GetValue("situation");
                situation = (Vessel.Situations)Enum.Parse(typeof(Vessel.Situations), situationStr);
            }
            catch (Exception e)
            {
                valid = false;
                Debug.LogError("ContractConfigurator: " + ErrorPrefix(configNode) +
                    ": error parsing situation: " + e.Message);
            }

            // Get title
            title = configNode.HasValue("title") ? configNode.GetValue("title") : null;

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ReachSituationCustom(situation, title);
        }
    }
}

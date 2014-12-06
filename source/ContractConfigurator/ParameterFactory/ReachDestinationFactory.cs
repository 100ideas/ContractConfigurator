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
     * ParameterFactory wrapper for ReachDestination ContractParameter.
     */
    public class ReachDestinationFactory : ParameterFactory
    {
        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            // Validate target body
            if (targetBody == null)
            {
                valid = false;
                Debug.LogError("ContractConfigurator: " + ErrorPrefix(configNode) +
                    ": targetBody for ReachDestination must be specified.");
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ReachDestinationCustom(targetBody, title);
        }
    }
}

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
    /// <summary>
    /// ParameterFactory wrapper for Rendezvous ContractParameter. 
    /// </summary>
    public class RendezvousFactory : ParameterFactory
    {
        protected List<string> vessels;
        protected double distance;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<List<string>>(configNode, "vessel", ref vessels, this, new List<string>());
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", ref distance, this, 2000.0);

            if (parent is VesselParameterGroupFactory)
            {
                if (vessels.Count > 1)
                {
                    LoggingUtil.LogError(this, ErrorPrefix() + ": When used under a VesselParameterGroup, no more than one vessel may be specified for the Rendezvous parameter.");
                    valid = false;
                }
            }
            else
            {
                if (vessels.Count == 0)
                {
                    LoggingUtil.LogError(this, ErrorPrefix() + ": Need at least one vessel specified for the Rendezvous parameter.");
                    valid = false;
                }
                if (vessels.Count > 2)
                {
                    LoggingUtil.LogError(this, ErrorPrefix() + ": Cannot specify more than two vessels for the Rendezvous parameter.");
                    valid = false;
                }
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new Rendezvous(vessels, distance, title);
        }
    }
}

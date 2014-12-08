﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;

namespace ContractConfigurator.Parameters
{
    /*
     * ContractParameter for use iwth the Sequence parameter.  Does not complete unless all
     * previous items in the sequence have completed.
     */
    public class SequenceNode : Contracts.ContractParameter
    {
        protected string title { get; set; }

        public SequenceNode()
            : this(null)
        {
        }

        public SequenceNode(string title)
            : base()
        {
            this.title = title;
        }

        protected override string GetTitle()
        {
            if (title != null && title != "")
            {
                return title;
            }
            else if (state == ParameterState.Complete)
            {
                if (ParameterCount == 1)
                {
                    return GetParameter(0).Title;
                }
                else
                {
                    return "Completed";
                }
            }
            else if (ReadyToComplete())
            {
                return "Complete the following";
            }
            else
            {
                return "Complete after the previous step";
            }
        }

        protected override string GetHashString()
        {
            return (this.Root.MissionSeed.ToString() + this.Root.DateAccepted.ToString() + this.ID);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("title", title);
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            title = node.GetValue("title");
        }

        /*
         * Checks if this parameter is ready to complete - in other words, all previous parameters
         * in the sequence have been completed.
         */
        private bool ReadyToComplete()
        {
            // Go through the parent's parameters
            Debug.Log("params for: " + ID);
            for (int i = 0; i < Parent.ParameterCount; i++)
            {
                ContractParameter param = Parent.GetParameter(i);
                Debug.Log("    param: " + param.ID + ", " + param.GetType().Name);
                // If we've made it all the way to us, we're ready
                if (System.Object.ReferenceEquals(param, this))
                {
                    Debug.Log("it's us!");
                    return true;
                }
                else if (param.State != ParameterState.Complete)
                {
                    Debug.Log("it's incomplete!");
                    return false;
                }
            }

            // Shouldn't get here unless things are really messed up
            Debug.LogWarning("ContractConfigurator - Unexpected state for SequenceNode parameter.  Log a GitHub issue!");
            return false;
        }

        protected override void OnParameterStateChange(ContractParameter contractParameter)
        {
            if (System.Object.ReferenceEquals(contractParameter.Parent, this))
            {
                if (AllChildParametersComplete())
                {
                    if (ReadyToComplete())
                    {
                        SetComplete();
                    }
                    else
                    {
                        SetIncomplete();
                    }
                }
                else if (AnyChildParametersFailed())
                {
                    SetFailed();
                }
            }
        }
    }
}

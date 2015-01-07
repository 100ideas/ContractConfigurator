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
    /// <summary>
    /// Parent classes must implement this interface, and should make the following call if
    /// ChildChanged is true:
    ///     GameEvents.Contract.onParameterChange.Fire(this.Root, this);
    /// </summary>
    public interface ParameterDelegateContainer
    {
        bool ChildChanged { get; set; }
    }

    /// <summary>
    /// Special parameter child class for filtering and validating a list of items.  Parent
    /// parameter classes MUST implement the ParameterDelegate.Container interface.
    /// </summary>
    /// <typeparam name="T">The type of item that will be validated.</typeparam>
    public class ParameterDelegate<T> : ContractParameter
    {
        protected enum MatchType
        {
            ANY,
            ALL,
            NONE
        }
        protected string title;
        protected Func<T, bool> filterFunc;
        protected bool trivial;

        public ParameterDelegate()
            : this(null, null)
        {
        }

        public ParameterDelegate(string title, Func<T, bool> filterFunc, bool trivial = false)
            : base()
        {
            this.title = title;
            this.filterFunc = filterFunc;
            this.trivial = trivial;
            disableOnStateChange = false;
        }

        protected override string GetTitle()
        {
            return title;
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("title", title);
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            title = ConfigNodeUtil.ParseValue<string>(node, "title");
        }

        protected void SetState(ParameterState newState)
        {
            if (state != newState)
            {
                LoggingUtil.LogVerbose(this, "Setting state for '" + title + "', state = " + newState);
                state = newState;
                ((ParameterDelegateContainer)Parent).ChildChanged = true;
            }
        }

        /// <summary>
        /// Apply the filter to the enumerator, and set our state based on the whether the
        /// incoming/outgoing values were empty.
        /// </summary>
        /// <param name="values">Enumerator to filter</param>
        /// <param name="fail">Whether there was an outright failure or the return value can be checked.</param>
        /// <returns>Enumerator after filtering</returns>
        protected virtual IEnumerable<T> SetState(IEnumerable<T> values, MatchType matchType, out bool fail, bool checkOnly = false)
        {
            fail = false;

            // Only checking, no state change allowed
            if (checkOnly)
            {
                return values.Where(filterFunc);
            }

            // Uncertain - return incomplete
            if (!values.Any() && matchType != MatchType.NONE)
            {
                SetState(ParameterState.Incomplete);
                return values;
            }

            // Apply the filter
            int count = values.Count();
            values = values.Where(filterFunc);

            // Some values - success
            if (matchType == MatchType.ALL ? values.Count() == count : values.Any())
            {
                SetState(matchType != MatchType.NONE ? ParameterState.Complete : ParameterState.Failed);
            }
            // No values - failure
            else
            {
                SetState(matchType != MatchType.NONE ? ParameterState.Failed : ParameterState.Complete);
            }

            return values;
        }

        /// <summary>
        /// Set the state for a single value of T.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <param name="checkOnly">Whether to actually set the state or just perform a check</param>
        /// <returns>Whether value met the criteria.</returns>
        protected virtual bool SetState(T value, bool checkOnly = false)
        {
            LoggingUtil.LogVerbose(this, "Checking condition for '" + title + "', value = " + value);

            bool result = filterFunc.Invoke(value);

            // Is state change allowed?
            if (!checkOnly)
            {
                SetState(result ? ParameterState.Complete : ParameterState.Failed);
            }

            return result;
        }

        /// <summary>
        /// To be called from the parent's OnLoad function.  Removes all child nodes, preventing
        /// stock logic from creating them.
        /// </summary>
        /// <param name="node">The config node to operate on.</param>
        public static void OnDelegateContainerLoad(ConfigNode node)
        {
            // No child parameters allowed!
            node.RemoveNodes("PARAM");
        }

        /// <summary>
        /// Checks the child conditions for each child parameter delegate in the given parent.
        /// </summary>
        /// <param name="param">The contract parameter that we are called from.</param>
        /// <param name="values">The values to enumerator over.</param>
        /// <param name="checkOnly">Only perform a check, don't change values.</param>
        /// <returns></returns>
        public static bool CheckChildConditions(ContractParameter param, IEnumerable<T> values, bool checkOnly = false)
        {
            bool fail = false;
            foreach (ContractParameter child in param.AllParameters)
            {
                if (child is ParameterDelegate<T>)
                {
                    ParameterDelegate<T> paramDelegate = (ParameterDelegate<T>)child;
                    LoggingUtil.LogVerbose(paramDelegate, "Checking condition for '" + paramDelegate.title + "', input.Any() = " + values.Any());
                    values = paramDelegate.SetState(values, MatchType.ANY, out fail, checkOnly);
                    LoggingUtil.LogVerbose(paramDelegate, "Checked condition for '" + paramDelegate.title + "', output.Any() = " + values.Any());
                }
            }

            return !fail && values.Any();
        }

        /// <summary>
        /// Checks the child conditions for each child parameter delegate in the given parent.
        /// </summary>
        /// <param name="param">The contract parameter that we are called from.</param>
        /// <param name="values">The values to enumerator over.</param>
        /// <param name="checkOnly">Only perform a check, don't change values.</param>
        /// <returns></returns>
        public static bool CheckChildConditions(ContractParameter param, T value, bool checkOnly = false)
        {
            bool conditionMet = true;
            foreach (ContractParameter child in param.AllParameters)
            {
                if (child is ParameterDelegate<T>)
                {
                    conditionMet &= ((ParameterDelegate<T>)child).SetState(value);
                }
            }

            return conditionMet;
        }

        /// <summary>
        /// Checks the child conditions for each child parameter delegate in the given parent, but
        /// checks that all 
        /// </summary>
        /// <param name="param">The contract parameter that we are called from.</param>
        /// <param name="values">The values to enumerator over.</param>
        /// <param name="checkOnly">Only perform a check, don't change values.</param>
        /// <returns></returns>
        public static bool CheckChildConditionsForAll(ContractParameter param, IEnumerable<T> values, bool checkOnly = false)
        {
            bool fail = false;
            bool conditionMet = true;
            int count = values.Count();
            foreach (ContractParameter child in param.AllParameters)
            {
                if (child is ParameterDelegate<T>)
                {
                    ParameterDelegate<T> paramDelegate = (ParameterDelegate<T>)child;
                    LoggingUtil.LogVerbose(paramDelegate, "Checking condition for '" + paramDelegate.title + "', input.Any() = " + values.Any());
                    IEnumerable<T> newValues = paramDelegate.SetState(values, MatchType.ALL, out fail, checkOnly);
                    LoggingUtil.LogVerbose(paramDelegate, "Checked condition for '" + paramDelegate.title + "', result = " + (count == newValues.Count()));
                    conditionMet &= count == newValues.Count();
                }
            }

            return !fail && conditionMet;
        }

        /// <summary>
        /// Checks the child conditions for each child parameter delegate in the given parent.
        /// </summary>
        /// <param name="param">The contract parameter that we are called from.</param>
        /// <param name="values">The values to enumerator over.</param>
        /// <param name="checkOnly">Only perform a check, don't change values.</param>
        /// <returns></returns>
        public static bool CheckChildConditionsForNone(ContractParameter param, IEnumerable<T> values, bool checkOnly = false)
        {
            bool fail = false;
            foreach (ContractParameter child in param.AllParameters)
            {
                if (child is ParameterDelegate<T>)
                {
                    ParameterDelegate<T> paramDelegate = (ParameterDelegate<T>)child;
                    LoggingUtil.LogVerbose(paramDelegate, "Checking condition for '" + paramDelegate.title + "', input.Any() = " + values.Any());
                    values = paramDelegate.SetState(values, MatchType.NONE, out fail, checkOnly);
                    LoggingUtil.LogVerbose(paramDelegate, "Checked condition for '" + paramDelegate.title + "', output.Any() = " + values.Any());
                }
            }

            return !fail && !values.Any();
        }
        /// <summary>
        /// Gets the text of all the child delegates in one big string.  Useful for printing out
        /// the full details for completed parameters.
        /// </summary>
        /// <param name="param">Th parent parameters.</param>
        /// <returns>The full delegate string</returns>
        public static string GetDelegateText(ContractParameter param)
        {
            string output = "";
            foreach (ContractParameter child in param.AllParameters)
            {
                if (child is ParameterDelegate<T> && !((ParameterDelegate<T>)child).trivial)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        output += "; ";
                    }
                    output += ((ParameterDelegate<T>)child).title;
                }
            }
            return output;
        }
    }

    /// <summary>
    /// Special ParameterDelegate class that counts the number of matches.
    /// </summary>
    /// <typeparam name="T">The type that will be enumerated over, ignored.</typeparam>
    public class CountParameterDelegate<T> : ParameterDelegate<T>
    {
        private int minCount;
        private int maxCount;

        public CountParameterDelegate(int minCount, int maxCount)
            : base("", x => true)
        {
            this.minCount = minCount;
            this.maxCount = maxCount;

            title = "Count: ";
            if (maxCount == 0)
            {
                title += "None";
            }
            else if (maxCount == int.MaxValue)
            {
                title += "At least " + minCount;
            }
            else if (minCount == 0)
            {
                title += "At most " + maxCount;
            }
            else if (minCount == maxCount)
            {
                title += "Exactly " + minCount;
            }
            else
            {
                title += "Between " + minCount + " and " + maxCount;
            }
        }

        protected override IEnumerable<T> SetState(IEnumerable<T> values, MatchType matchType, out bool fail, bool checkOnly = false)
        {
            // Set our state
            int count = values.Count();
            bool conditionMet = count >= minCount && count <= maxCount;
            if (!checkOnly)
            {
                SetState(conditionMet ? ParameterState.Complete : ParameterState.Incomplete);
            }

            fail = !conditionMet;

            return values;
        }
    }
}

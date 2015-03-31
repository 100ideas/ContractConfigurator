﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP;
using Contracts;
using ContractConfigurator.Parameters;

namespace ContractConfigurator
{
    /// <summary>
    /// Static class with extensions to stock classes.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets all the parameter's descendents
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ContractParameter> GetAllDescendents(this IContractParameterHost p)
        {
            for (int i = 0; i < p.ParameterCount; i++)
            {
                ContractParameter child = p.GetParameter(i);
                yield return child;
                foreach (ContractParameter descendent in child.GetAllDescendents())
                {
                    yield return descendent;
                }
            }
        }

        /// <summary>
        /// Gets all the parameter's descendents
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ContractParameter> GetChildren(this IContractParameterHost p)
        {
            for (int i = 0; i < p.ParameterCount; i++)
            {
                yield return p.GetParameter(i);
            }
        }

        /// <summary>
        /// Gets all the kerbals for the given roster.
        /// </summary>
        /// <param name="p">Contract parameter</param>
        /// <returns>Enumerator of descendents</returns>
        public static IEnumerable<ProtoCrewMember> AllKerbals(this KerbalRoster roster)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                yield return roster[i];
            }
        }

        /// <summary>
        /// Gets the quantity of the given resource for the vessel.
        /// </summary>
        /// <param name="vessel">Vessel to check</param>
        /// <param name="resource">Resource to check for</param>
        /// <returns></returns>
        public static double ResourceQuantity(this Vessel vessel, PartResourceDefinition resource)
        {
            if (vessel == null)
            {
                return 0.0;
            }

            double quantity = 0.0;
            foreach (Part part in vessel.Parts)
            {
                PartResource pr = part.Resources[resource.name];
                if (pr != null)
                {
                    quantity += pr.amount;
                }
            }
            return quantity;
        }

        /// <summary>
        /// Create a hash of the vessel.
        /// </summary>
        /// <param name="vessel">The vessel to hash</param>
        /// <returns>A list of hashes for this vessel</returns>
        public static IEnumerable<uint> GetHashes(this Vessel vessel)
        {
            if (vessel.rootPart == null)
            {
                yield break;
            }

            Queue<Part> queue = new Queue<Part>();
            Dictionary<Part, int> visited = new Dictionary<Part, int>();
            Dictionary<uint, uint> dockedParts = new Dictionary<uint, uint>();
            Queue<Part> otherVessel = new Queue<Part>();

            // Add the root
            queue.Enqueue(vessel.rootPart);
            visited[vessel.rootPart] = 1;

            // Do a BFS of all parts.
            uint hash = 0;
            while (queue.Count > 0 || otherVessel.Count > 0)
            {
                bool decoupler = false;

                // Start a new ship
                if (queue.Count == 0)
                {
                    // Reset our hash
                    yield return hash;
                    hash = 0;

                    // Find an unhandled part to use as the new vessel
                    Part px;
                    while (px = otherVessel.Dequeue())
                    {
                        if (visited[px] != 2)
                        {
                            queue.Enqueue(px);
                            break;
                        }
                    }
                    dockedParts.Clear();
                    continue;
                }

                Part p = queue.Dequeue();

                // Check if this is for a new vessel
                if (dockedParts.ContainsKey(p.flightID))
                {
                    otherVessel.Enqueue(p);
                    continue;
                }

                // Special handling of certain modules
                for (int i = 0; i < p.Modules.Count; i++)
                {
                    PartModule pm = p.Modules.GetModule(i);

                    // If this is a docking node, track the docked part
                    if (pm.moduleName == "ModuleDockingNode")
                    {
                        ModuleDockingNode dock = (ModuleDockingNode)pm;
                        if (dock.dockedPartUId != 0)
                        {
                            dockedParts[dock.dockedPartUId] = dock.dockedPartUId;
                        }
                    }
                    else if (pm.moduleName == "ModuleDecouple")
                    {
                        // Just assume all parts can decouple from this, it's easier and
                        // effectively the same thing
                        decoupler = true;

                        // Parent may be null if this is the root of the stack
                        if (p.parent != null)
                        {
                            dockedParts[p.parent.flightID] = p.parent.flightID;
                        }

                        // Add all children as possible new vessels
                        foreach (Part child in p.children)
                        {
                            dockedParts[child.flightID] = child.flightID;
                        }
                    }
                }

                // Go through our child parts
                foreach (Part child in p.children)
                {
                    if (!visited.ContainsKey(child))
                    {
                        queue.Enqueue(child);
                        visited[child] = 1;
                    }
                }

                // Confirm if parent part has been visited
                if (p.parent != null && !visited.ContainsKey(p.parent))
                {
                    queue.Enqueue(p.parent);
                    visited[p.parent] = 1;
                }

                // Add this part to the hash
                if (!decoupler)
                {
                    hash ^= p.flightID;
                }

                // We've processed this node
                visited[p] = 2;
            }

            // Return the last hash
            yield return hash;
        }
    }
}

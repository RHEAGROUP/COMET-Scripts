// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataReadService.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2018 RHEA System S.A.
//
//    Author: Sam Gerené, Merlin Bieze, Alex Vorobiev, Naron Phou
//
//    This file is part of CDP4-SDK Community Edition
//
//    The CDP4-SDK Community Edition is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 3 of the License, or (at your option) any later version.
//
//    The CDP4-SDK Community Edition is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public License
//    along with this program; if not, write to the Free Software Foundation,
//    Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace CDP4Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CDP4Common;
    using CDP4Common.CommonData;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.Types;
    using CDP4Dal;

    /// <summary>
    /// A service that that returns engineering-model data based on the current <see cref="ISession"/>
    /// </summary>
    public class DataReadService
    {
        /// <summary>
        /// The current <see cref="ISession"/>
        /// </summary>
        private readonly ISession session;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReadService"/> class
        /// </summary>
        /// <param name="session">The current session</param>
        internal DataReadService(ISession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Gets a <see cref="ElementDefinition"/> or <see cref="ElementUsage"/> by its model-code
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelcode">The modle-code</param>
        /// <returns>The <see cref="ElementBase"/></returns>
        public ElementBase GetElementByModelCode(Iteration iteration, string modelcode)
        {
            return this.session.Assembler.Cache
                .Values
                .Select(x => x.Value)
                .OfType<ElementBase>()
                .Where(x =>
                {
                    var modelcodething = (IModelCode)x;
                    return x.CacheId.Item2.HasValue
                        && x.CacheId.Item2.Value == iteration.Iid
                        && modelcodething.ModelCode().Equals(modelcode, StringComparison.InvariantCultureIgnoreCase);
                }
                )
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets a CDP4 <see cref="ParameterBase"/> by its model-code for the current domain
        /// The returned <see cref="ParameterBase"/> is the <see cref="ParameterSubscription"/> for the current domain if it exists
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model code</param>
        /// <param name="getDomainValue">indicates whether the <see cref="ParameterSubscription"/> shall be returned if it exists</param>
        /// <returns>The <see cref="IModelCode"/> thing</returns>
        public ParameterBase GetParameterByModelCode(Iteration iteration, string modelCode, bool getDomainValue)
        {
            var things = this.session.Assembler.Cache
                .Values
                .Select(x => x.Value)
                .OfType<ParameterBase>()
                .Where(x =>
                        x.CacheId.Item2.HasValue
                           && x.CacheId.Item2.Value == iteration.Iid
                           && x.ModelCode().Equals(modelCode, StringComparison.InvariantCultureIgnoreCase)
                )
                .ToArray();

            if (things.Length == 0)
            {
                return null;
            }

            if (things.Length == 1)
            {
                return things.Single();
            }

            // there should only be multiple thing with same model-id for parameter/override and their subscriptions
            var domain = this.session.OpenIterations.SingleOrDefault(x => x.Key.Iid == iteration.Iid).Value?.Item1;
            if (domain == null || !getDomainValue)
            {
                return things.OfType<ParameterOrOverrideBase>().SingleOrDefault();
            }

            var domainSubscription = things.OfType<ParameterSubscription>().FirstOrDefault(x => x.Owner.Iid == domain.Iid);
            return (ParameterBase)domainSubscription ?? things.OfType<ParameterOrOverrideBase>().SingleOrDefault();
        }

        /// <summary>
        /// Gets a CDP4 <see cref="IValueSet"/> by its model-code for the current domain
        /// The returned <see cref="IValueSet"/> is the <see cref="ParameterSubscription"/> for the current domain if it exists
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model code</param>
        /// <param name="getDomainValue">indicates whether the <see cref="ParameterSubscriptionValueSet"/> shall be returned if it exists</param>
        /// <returns>The <see cref="IModelCode"/> thing</returns>
        public IValueSet GetValueSetByModelCode(Iteration iteration, string modelCode, bool getDomainValue)
        {
            var things = this.session.Assembler.Cache
                .Values
                .Select(x => x.Value)
                .OfType<IValueSet>()
                .Where(
                    x =>
                    {
                        var thing = (Thing)x;
                        return thing.CacheId.Item2.HasValue
                            && thing.CacheId.Item2.Value == iteration.Iid
                            && this.GetAllParameterModelCode(x).Any(m => m.Equals(modelCode, StringComparison.InvariantCultureIgnoreCase));
                    }
                )
                .ToArray();

            if (things.Length == 0)
            {
                return null;
            }

            if (things.Length == 1)
            {
                return things.Single();
            }

            // there should only be multiple thing with same model-id for parameter/override and their subscriptions
            var domain = this.session.OpenIterations.SingleOrDefault(x => x.Key.Iid == iteration.Iid).Value?.Item1;
            if (domain == null || !getDomainValue)
            {
                return things.OfType<ParameterValueSetBase>().SingleOrDefault();
            }

            var domainSubscription = things.OfType<ParameterSubscriptionValueSet>().FirstOrDefault(x => x.Owner.Iid == domain.Iid);
            return (IValueSet)domainSubscription ?? things.OfType<ParameterValueSetBase>().SingleOrDefault();
        }

        /// <summary>
        /// Gets the actual value of the <see cref="IValueSet"/> with the given <paramref name="modelCode"/>
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <returns>The actual value</returns>
        public string GetDomainActualValue(Iteration iteration, string modelCode)
        {
            return this.GetValue(iteration, modelCode, true, set => set.ActualValue);
        }

        /// <summary>
        /// Gets the manual value of the <see cref="IValueSet"/> with the given <paramref name="modelCode"/>
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <returns>The actual value</returns>
        public string GetDomainManualValue(Iteration iteration, string modelCode)
        {
            return this.GetValue(iteration, modelCode, true, set => set.Manual);
        }

        /// <summary>
        /// Gets the computed value of the <see cref="IValueSet"/> with the given <paramref name="modelCode"/>
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <returns>The actual value</returns>
        public string GetDomainComputedValue(Iteration iteration, string modelCode)
        {
            return this.GetValue(iteration, modelCode, true, set => set.Computed);
        }

        /// <summary>
        /// Gets the reference value of the <see cref="IValueSet"/> with the given <paramref name="modelCode"/>
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <returns>The actual value</returns>
        public string GetDomainReferencedValue(Iteration iteration, string modelCode)
        {
            return this.GetValue(iteration, modelCode, true, set => set.Reference);
        }

        /// <summary>
        /// Gets the actual value of the <see cref="IValueSet"/> with the given <paramref name="modelCode"/>
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <returns>The actual value</returns>
        public string GetPublishedValue(Iteration iteration, string modelCode)
        {
            // not getting subscription here as we are interested in the published value only
            if (!(this.GetValueSetByModelCode(iteration, modelCode, false) is ParameterValueSetBase thing))
            {
                throw new Cdp4ScriptException($"The parameter or overide value-set with model-code {modelCode} was not found.");
            }

            var parameterBase = (ParameterBase)(thing).Container;
            var value = "";
            for (var i = 0; i < parameterBase.ParameterType.NumberOfValues; i++)
            {
                if (thing.ModelCode(i) == modelCode)
                {
                    value = thing.Published[i];
                }
            }

            return value;
        }

        /// <summary>
        /// Gets the value with the given <paramref name="modelCode"/> in the specified <paramref name="valuearray"/>
        /// </summary>
        /// <param name="iteration">The current iteration</param>
        /// <param name="modelCode">The model-code</param>
        /// <param name="getDomainValue">Specify whether the domain (subscription if exists) shall be used</param>
        /// <param name="valuearray">The value array to get the value from</param>
        /// <returns>The value</returns>
        private string GetValue(Iteration iteration, string modelCode, bool getDomainValue, Func<IValueSet, ValueArray<string>> valuearray)
        {
            var thing = this.GetValueSetByModelCode(iteration, modelCode, getDomainValue);
            if (thing == null)
            {
                throw new Cdp4ScriptException($"The value-set with model-code {modelCode} was not found.");
            }

            var parameterBase = (ParameterBase)((Thing)thing).Container;
            var value = "";
            for (var i = 0; i < parameterBase.ParameterType.NumberOfValues; i++)
            {
                if (thing.ModelCode(i) == modelCode)
                {
                    value = valuearray(thing)[i];
                }
            }

            return value;
        }

        /// <summary>
        /// Gets all model-code of a <see cref="IModelCode"/>
        /// </summary>
        /// <param name="modelCodeThing">The <see cref="IModelCode"/> thing</param>
        /// <returns>The codes</returns>
        /// <remarks>
        /// Only a value-set for a compound-parameter-type has more than 1 model-code
        /// </remarks>
        private IEnumerable<string> GetAllParameterModelCode(IModelCode modelCodeThing)
        {
            var valueset = modelCodeThing as IValueSet;
            if (valueset == null)
            {
                return new[] { modelCodeThing.ModelCode() };
            }

            // for a value-set its applicable model-code is all of them (in case of compound parameter-type)
            var parameterContainer = (ParameterBase)((Thing)modelCodeThing).Container;
            var modelCodes = new string[parameterContainer.ParameterType.NumberOfValues];
            for (var i = 0; i < parameterContainer.ParameterType.NumberOfValues; i++)
            {
                modelCodes[i] = modelCodeThing.ModelCode(i);
            }

            return modelCodes;
        }
    }
}

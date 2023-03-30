// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BaseService.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2018 RHEA System S.A.
//
//    Author: Sam Gerené, Merlin Bieze, Alex Vorobiev, Naron Phou
//
//    This file is part of CDP4-SDK Community Edition
//
//    The CDP4-Scripts Community Edition is free software; you can redistribute it and/or
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
    using CDP4Dal;
    using CDP4Dal.Permission;

    /// <summary>
    /// The abstract type for all services
    /// </summary>
    public abstract class BaseService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseService"/> class
        /// </summary>
        /// <param name="session">The current <see cref="ISession"/></param>
        /// <param name="permissionService">The <see cref="IPermissionService"/></param>
        protected BaseService(ISession session, IPermissionService permissionService)
        {
            this.Session = session;
            this.PermissionService = permissionService;
        }

        /// <summary>
        /// Gets the <see cref="IPermissionService"/>
        /// </summary>
        protected IPermissionService PermissionService { get; }

        /// <summary>
        /// Gets the <see cref="ISession"/>
        /// </summary>
        protected ISession Session { get; }

        /// <summary>
        /// Gets a CDP4 <see cref="IValueSet"/> by its model-code for the current domain
        /// The returned <see cref="IValueSet"/> is the <see cref="ParameterSubscription"/> for the current domain if it exists
        /// </summary>
        /// <param name="iteration">The iteration</param>
        /// <param name="modelCode">The model code</param>
        /// <param name="getDomainValue">indicates whether the <see cref="ParameterSubscriptionValueSet"/> shall be returned if it exists</param>
        /// <returns>The <see cref="IModelCode"/> thing</returns>
        protected IValueSet ProtectedGetValueSetByModelCode(Iteration iteration, string modelCode, bool getDomainValue)
        {
            var things = this.Session.Assembler.Cache
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
            var domain = this.Session.OpenIterations.SingleOrDefault(x => x.Key.Iid == iteration.Iid).Value?.Item1;
            if (domain == null || !getDomainValue)
            {
                return things.OfType<ParameterValueSetBase>().SingleOrDefault();
            }

            var domainSubscription = things.OfType<ParameterSubscriptionValueSet>().FirstOrDefault(x => x.Owner.Iid == domain.Iid);
            return (IValueSet)domainSubscription ?? things.OfType<ParameterValueSetBase>().SingleOrDefault();
        }

        /// <summary>
        /// Gets all model-code of a <see cref="IModelCode"/>
        /// </summary>
        /// <param name="modelCodeThing">The <see cref="IModelCode"/> thing</param>
        /// <returns>The codes</returns>
        /// <remarks>
        /// Only a value-set for a compound-parameter-type has more than 1 model-code
        /// </remarks>
        protected IEnumerable<string> GetAllParameterModelCode(IModelCode modelCodeThing)
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

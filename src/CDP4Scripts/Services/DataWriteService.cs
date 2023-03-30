// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataWriteService.cs" company="RHEA System S.A.">
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
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;
    using CDP4Common.Validation;
    using CDP4Dal;
    using CDP4Dal.Operations;
    using CDP4Dal.Permission;
    using Parameter = CDP4Common.DTO.Parameter;

    /// <summary>
    /// A service that provides functionality to write in the database
    /// </summary>
    public class DataWriteService : BaseService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataWriteService"/> class
        /// </summary>
        /// <param name="session">The current session</param>
        /// <param name="permissionService">The <see cref="IPermissionService"/></param>
        internal DataWriteService(ISession session, IPermissionService permissionService) : base(session, permissionService)
        {
        }

        /// <summary>
        /// Update the manual value
        /// </summary>
        /// <param name="iteration">The current iteration</param>
        /// <param name="modelCode">The model-code of the value to update</param>
        /// <param name="newvalue">The new-value</param>
        /// <remarks>
        /// To update a subscription this method shall always be used
        /// </remarks>
        public void UpdateDomainManualValue(Iteration iteration, string modelCode, string newvalue)
        {
            this.UpdateParameterValue(iteration, modelCode, newvalue, ParameterSwitchKind.MANUAL);
        }

        /// <summary>
        /// Update the reference value
        /// </summary>
        /// <param name="iteration">The current iteration</param>
        /// <param name="modelCode">The model-code of the value to update</param>
        /// <param name="newvalue">The new-value</param>
        public void UpdateDomainReferenceValue(Iteration iteration, string modelCode, string newvalue)
        {
            this.UpdateParameterValue(iteration, modelCode, newvalue, ParameterSwitchKind.REFERENCE);
        }

        /// <summary>
        /// Update the computed value
        /// </summary>
        /// <param name="iteration">The current iteration</param>
        /// <param name="modelCode">The model-code of the value to update</param>
        /// <param name="newvalue">The new-value</param>
        public void UpdateDomainComputedValue(Iteration iteration, string modelCode, string newvalue)
        {
            this.UpdateParameterValue(iteration, modelCode, newvalue, ParameterSwitchKind.COMPUTED);
        }

        /// <summary>
        /// Update a <see cref="IValueSet"/> for a given switch
        /// </summary>
        /// <param name="iteration">The current iteration</param>
        /// <param name="modelCode">The model-code of the value to update</param>
        /// <param name="newvalue">The updated-value</param>
        /// <param name="parameterSwitch">The switch to use</param>
        private void UpdateParameterValue(Iteration iteration, string modelCode, string newvalue, ParameterSwitchKind parameterSwitch = ParameterSwitchKind.MANUAL)
        {
            var valueset = this.ProtectedGetValueSetByModelCode(iteration, modelCode, true);
            var valuesetThing = (Thing)valueset;
            var domain = this.Session.OpenIterations.SingleOrDefault(x => x.Key.Iid == iteration.Iid).Value?.Item1;

            if (!(valueset is IOwnedThing ownedThing) || domain == null || ownedThing.Owner.Iid != domain.Iid || !this.PermissionService.CanWrite(valuesetThing))
            {
                // give permission for a power-user?
                throw new Cdp4ScriptException("You do not have permission or you don't represent the domain of the value-set to update.");
            }

            if (!(valuesetThing.Container is ParameterBase parameterBase))
            {
                throw new Cdp4ScriptException("The container of the value-set is null.");
            }

            var clone = valuesetThing.Clone(false);
            var transaction = new ThingTransaction(TransactionContextResolver.ResolveContext(clone));

            if (parameterBase.ParameterType is CompoundParameterType compoundPt)
            {
                if (compoundPt.Component.Count != valueset.ActualValue.Count)
                {
                    throw new Cdp4ScriptException("The number of values do not match the number of components in the compound parameter to update.");
                }

                for (var i = 0; i < valueset.ActualValue.Count; i++)
                {
                    var modelcode = valueset.ModelCode(i);
                    if (!string.Equals(modelcode, modelCode, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var scale = compoundPt.Component[i].Scale;
                    var pt = compoundPt.Component[i].ParameterType;
                    var validationResult = pt.Validate(newvalue, scale);
                    if (validationResult.ResultKind != ValidationResultKind.Valid)
                    {
                        throw new Cdp4ScriptException($"{validationResult.ResultKind.ToString()} value ({newvalue}) for the parameter with model-code {modelCode}: {validationResult.Message}.");
                    }

                    this.SetCompoundValue((IValueSet)clone, i, this.GetValueSet(valueset, parameterSwitch), newvalue, parameterSwitch);
                    break;
                }
            }
            else
            {
                var validationResult = parameterBase.ParameterType.Validate(newvalue, parameterBase.Scale);
                if (validationResult.ResultKind != ValidationResultKind.Valid)
                {
                    throw new Cdp4ScriptException($"{validationResult.ResultKind.ToString()} value ({newvalue}) for the parameter with model-code {modelCode}: {validationResult.Message}.");
                }

                this.SetValue((IValueSet)clone, new [] { newvalue }, parameterSwitch );
            }

            transaction.CreateOrUpdate(clone);
            this.Write(transaction);
        }


        /// <summary>
        /// Set the value of a <see cref="IValueSet"/>
        /// </summary>
        /// <param name="valueset">The <see cref="IValueSet"/></param>
        /// <param name="newvalue">The updated value-array containing the values of the <see cref="IValueSet"/> to update</param>
        /// <param name="parameterSwitch">The switch that determine which value of the <see cref="IValueSet"/> to update</param>
        private void SetValue(IValueSet valueset, IEnumerable<string> newvalue, ParameterSwitchKind parameterSwitch = ParameterSwitchKind.MANUAL)
        {
            if (valueset is ParameterValueSetBase parameterValueSetBase)
            {
                this.SetValue(parameterValueSetBase, parameterSwitch, newvalue);
            }
            else if (valueset is ParameterSubscriptionValueSet parameterSubscriptionValueSet)
            {
                this.SetValue(parameterSubscriptionValueSet, newvalue);
            }
            else
            {
                throw new Cdp4ScriptException("Vallue-set is neither of type ParameterValueSetBase or ParameterSubscriptionValueSet");
            }
        }

        /// <summary>
        /// Set the value of a <see cref="ParameterValueSetBase"/>
        /// </summary>
        /// <param name="valueset">The <see cref="ParameterValueSetBase"/></param>
        /// <param name="newvalue">The updated value-array containing the values of the <see cref="ParameterValueSetBase"/> to update</param>
        /// <param name="parameterSwitch">The switch that determine which value of the <see cref="ParameterValueSetBase"/> to update</param>
        private void SetValue(ParameterValueSetBase valueset, ParameterSwitchKind parameterSwitch, IEnumerable<string> newvalue)
        {
            switch (parameterSwitch)
            {
                case ParameterSwitchKind.MANUAL:
                    valueset.Manual = new ValueArray<string>(newvalue);
                    break;
                case ParameterSwitchKind.COMPUTED:
                    valueset.Computed = new ValueArray<string>(newvalue);
                    break;
                case ParameterSwitchKind.REFERENCE:
                    valueset.Reference = new ValueArray<string>(newvalue);
                    break;
                default:
                    throw new Cdp4ScriptException($"Cannot set value for switch {parameterSwitch.ToString()}");
            }
        }

        /// <summary>
        /// Set the value of a <see cref="ParameterSubscriptionValueSet"/>
        /// </summary>
        /// <param name="valueset">The <see cref="ParameterSubscriptionValueSet"/></param>
        /// <param name="newvalue">The updated value-array containing the values of the <see cref="ParameterValueSetBase"/> to update</param>
        private void SetValue(ParameterSubscriptionValueSet valueset, IEnumerable<string> newvalue)
        {
            valueset.Manual = new ValueArray<string>(newvalue);
        }

        /// <summary>
        /// Set the value of a <paramref name="valueset"/> when the parameter type is a <see cref="CompoundParameterType"/>
        /// </summary>
        /// <param name="valueset">The <see cref="IValueSet"/> to update</param>
        /// <param name="i">The index representing the component to update</param>
        /// <param name="oldvalues">the old-values</param>
        /// <param name="newvalue">The updated value for the component</param>
        /// <param name="parameterSwitch">The switch</param>
        private void SetCompoundValue(IValueSet valueset, int i, ValueArray<string> oldvalues, string newvalue, ParameterSwitchKind parameterSwitch = ParameterSwitchKind.MANUAL)
        {
            var newvalues = oldvalues.ToList();
            newvalues[i] = newvalue;

            this.SetValue(valueset, newvalues, parameterSwitch);
        }

        /// <summary>
        /// Gets the values from the <paramref name="valueset"/> based on the <paramref name="parameterSwitch"/>
        /// </summary>
        /// <param name="valueset">The <see cref="IValueSet"/></param>
        /// <param name="parameterSwitch">The <see cref="ParameterSwitchKind"/></param>
        /// <returns>The <see cref="ValueArray{String}"/></returns>
        private ValueArray<string> GetValueSet(IValueSet valueset, ParameterSwitchKind parameterSwitch = ParameterSwitchKind.MANUAL)
        {
            switch (parameterSwitch)
            {
                case ParameterSwitchKind.MANUAL:
                    return valueset.Manual;
                case ParameterSwitchKind.COMPUTED:
                    return valueset.Computed;
                case ParameterSwitchKind.REFERENCE:
                    return valueset.Reference;
                default:
                    throw new Cdp4ScriptException($"Cannot get value for switch {parameterSwitch.ToString()}");
            }
        }

        /// <summary>
        /// Finalize the write operation
        /// </summary>
        /// <param name="transaction">The <see cref="ThingTransaction"/></param>
        private void Write(ThingTransaction transaction)
        {
            try
            {
                this.Session.Write(transaction.FinalizeTransaction()).Wait();
            }
            catch (Exception e)
            {
                throw new Cdp4ScriptException($"An error occured during the POST operation: {e.Message}.");
            }
        }
    }
}

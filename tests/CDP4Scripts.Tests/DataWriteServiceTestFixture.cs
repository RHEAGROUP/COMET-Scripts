// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataWriteServiceTestFixture.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2018 RHEA System S.A.
//
//    Author: Sam Gerené, Merlin Bieze, Alex Vorobiev, Naron Phou
//
//    This file is part of CDP4Scripts Community Edition
//
//    The CDP4Scripts Community Edition is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 3 of the License, or (at your option) any later version.
//
//    The CDP4Scripts Community Edition is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public License
//    along with this program; if not, write to the Free Software Foundation,
//    Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace CDP4ServicesForPython.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CDP4Common.CommonData;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;
    using CDP4Dal;
    using CDP4Dal.DAL;
    using CDP4Dal.Operations;
    using CDP4Dal.Permission;
    using CDP4Scripts;
    using NUnit.Framework;
    using CDP4ServicesForPython;
    using Moq;

    [TestFixture]
    public class DataWriteServiceTestFixture
    {
        private Mock<ISession> session;
        private Mock<IPermissionService> permissionService;

        private DataWriteService service;

        private Iteration iteration;
        private DomainOfExpertise domain1;
        private DomainOfExpertise domain2;

        private readonly Uri uri = new Uri("http://test.com");

        [SetUp]
        public void Setup()
        {
            this.session = new Mock<ISession>();
            this.permissionService = new Mock<IPermissionService>();
            this.permissionService.Setup(x => x.CanWrite(It.IsAny<Thing>())).Returns(true);
            var assembler = new Assembler(this.uri);
            this.session.Setup(x => x.Assembler).Returns(assembler);

            #region site-dir
            var sitedir = new SiteDirectory(Guid.NewGuid(), assembler.Cache, this.uri);
            this.domain1 = new DomainOfExpertise(Guid.NewGuid(), assembler.Cache, this.uri);
            this.domain2 = new DomainOfExpertise(Guid.NewGuid(), assembler.Cache, this.uri);

            var modelsetup = new EngineeringModelSetup(Guid.NewGuid(), assembler.Cache, this.uri) { EngineeringModelIid = Guid.NewGuid() };
            var iterationsetup = new IterationSetup(Guid.NewGuid(), assembler.Cache, this.uri) { IterationIid = Guid.NewGuid() };

            sitedir.Model.Add(modelsetup);
            modelsetup.IterationSetup.Add(iterationsetup);

            sitedir.Domain.Add(this.domain1);
            sitedir.Domain.Add(this.domain2);

            var scale = new RatioScale(Guid.NewGuid(), assembler.Cache, this.uri) { NumberSet = NumberSetKind.REAL_NUMBER_SET };
            var scalar1 = new SimpleQuantityKind(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "scalar1", DefaultScale = scale};
            var scalar2 = new SimpleQuantityKind(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "scalar2", DefaultScale = scale};
            var compound = new CompoundParameterType(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "compound" };
            var cpt1 = new ParameterTypeComponent(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "cpt1", ParameterType = scalar1 };
            var cpt2 = new ParameterTypeComponent(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "cpt2", ParameterType = scalar1 };

            compound.Component.Add(cpt1);
            compound.Component.Add(cpt2);

            var srdl = new SiteReferenceDataLibrary(Guid.NewGuid(), assembler.Cache, this.uri);
            srdl.ParameterType.Add(scalar1);
            srdl.ParameterType.Add(scalar2);
            srdl.ParameterType.Add(compound);
            sitedir.SiteReferenceDataLibrary.Add(srdl);

            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(sitedir.Iid, null), new Lazy<Thing>(() => sitedir));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(this.domain1.Iid, null), new Lazy<Thing>(() => this.domain1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(this.domain2.Iid, null), new Lazy<Thing>(() => this.domain2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(modelsetup.Iid, null), new Lazy<Thing>(() => modelsetup));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(iterationsetup.Iid, null), new Lazy<Thing>(() => iterationsetup));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(scalar1.Iid, null), new Lazy<Thing>(() => scalar1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(scalar2.Iid, null), new Lazy<Thing>(() => scalar2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(compound.Iid, null), new Lazy<Thing>(() => compound));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(cpt1.Iid, null), new Lazy<Thing>(() => cpt1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(cpt2.Iid, null), new Lazy<Thing>(() => cpt2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(srdl.Iid, null), new Lazy<Thing>(() => srdl));
            #endregion

            #region model-data
            var model = new EngineeringModel(modelsetup.EngineeringModelIid, assembler.Cache, this.uri);
            this.iteration = new Iteration(iterationsetup.IterationIid, assembler.Cache, this.uri);
            var option = new Option(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "opt" };
            var pl = new PossibleFiniteStateList(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "pfsl" };
            var ps1 = new PossibleFiniteState(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "ps1" };
            var ps2 = new PossibleFiniteState(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "ps2" };
            var al = new ActualFiniteStateList(Guid.NewGuid(), assembler.Cache, this.uri);
            al.PossibleFiniteStateList.Add(pl);
            var as1 = new ActualFiniteState(Guid.NewGuid(), assembler.Cache, this.uri);
            as1.PossibleState.Add(ps1);
            var as2 = new ActualFiniteState(Guid.NewGuid(), assembler.Cache, this.uri);
            as2.PossibleState.Add(ps2);

            var ed1 = new ElementDefinition(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "ed1" };
            var ed2 = new ElementDefinition(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "ed2" };
            var us = new ElementUsage(Guid.NewGuid(), assembler.Cache, this.uri) { ShortName = "usage", ElementDefinition = ed2 };

            var p1 = new Parameter(Guid.NewGuid(), assembler.Cache, this.uri) { ParameterType = scalar1, Owner = this.domain1, Scale = scale};
            var v1 = new ParameterValueSet(Guid.NewGuid(), assembler.Cache, this.uri);
            v1.Manual = new ValueArray<string>(new [] { "m1" });
            v1.Published = new ValueArray<string>(new [] { "p1" });
            v1.Computed = new ValueArray<string>(new [] { "c1" });
            v1.Reference = new ValueArray<string>(new [] { "r1" });
            v1.ValueSwitch = ParameterSwitchKind.MANUAL;

            p1.ValueSet.Add(v1);

            var sub = new ParameterSubscription(Guid.NewGuid(), assembler.Cache, this.uri) { Owner = this.domain2 };
            var vs = new ParameterSubscriptionValueSet(Guid.NewGuid(), assembler.Cache, this.uri) { SubscribedValueSet = v1};
            vs.Manual = new ValueArray<string>(new[] { "s-m1" });
            vs.ValueSwitch = ParameterSwitchKind.MANUAL;
            sub.ValueSet.Add(vs);

            p1.ParameterSubscription.Add(sub);

            var o1 = new ParameterOverride(Guid.NewGuid(), assembler.Cache, this.uri) { Parameter = p1, Owner = this.domain1 };
            var ov1 = new ParameterOverrideValueSet(Guid.NewGuid(), assembler.Cache, this.uri) { ParameterValueSet = v1 };
            ov1.Manual = new ValueArray<string>(new[] { "o-m1" });
            ov1.Published = new ValueArray<string>(new[] { "o-p1" });
            ov1.Computed = new ValueArray<string>(new[] { "o-c1" });
            ov1.Reference = new ValueArray<string>(new[] { "o-r1" });
            ov1.ValueSwitch = ParameterSwitchKind.MANUAL;
            o1.ValueSet.Add(ov1);

            var p2 = new Parameter(Guid.NewGuid(), assembler.Cache, this.uri) { ParameterType = scalar2, IsOptionDependent = true, StateDependence = al, Owner = this.domain1, Scale = scale };
            var v21 = new ParameterValueSet(Guid.NewGuid(), assembler.Cache, this.uri) { ActualOption = option, ActualState = as1 };
            var v22 = new ParameterValueSet(Guid.NewGuid(), assembler.Cache, this.uri) { ActualOption = option, ActualState = as2 };

            v21.Manual = new ValueArray<string>(new[] { "m2-s1" });
            v21.Published = new ValueArray<string>(new[] { "p2-s1" });
            v21.Computed = new ValueArray<string>(new[] { "c2-s1" });
            v21.Reference = new ValueArray<string>(new[] { "r2-s1" });
            v21.ValueSwitch = ParameterSwitchKind.MANUAL;

            v22.Manual = new ValueArray<string>(new[] { "m2-s2" });
            v22.Published = new ValueArray<string>(new[] { "p2-s2" });
            v22.Computed = new ValueArray<string>(new[] { "c2-s2" });
            v22.Reference = new ValueArray<string>(new[] { "r2-s2" });
            v22.ValueSwitch = ParameterSwitchKind.MANUAL;

            p2.ValueSet.Add(v21);
            p2.ValueSet.Add(v22);

            var p3 = new Parameter(Guid.NewGuid(), assembler.Cache, this.uri) { ParameterType = compound, Owner = this.domain1, Scale = scale };
            var v3 = new ParameterValueSet(Guid.NewGuid(), assembler.Cache, this.uri);
            v3.Manual = new ValueArray<string>(new[] { "m31", "m32" });
            v3.Published = new ValueArray<string>(new[] { "p31", "p32" });
            v3.Computed = new ValueArray<string>(new[] { "c31", "c32" });
            v3.Reference = new ValueArray<string>(new[] { "r31", "r32" });
            v3.ValueSwitch = ParameterSwitchKind.MANUAL;

            p3.ValueSet.Add(v3);

            ed1.Parameter.Add(p2);
            ed1.Parameter.Add(p3);
            ed2.Parameter.Add(p1);

            us.ParameterOverride.Add(o1);

            ed1.ContainedElement.Add(us);
            this.iteration.Element.Add(ed1);
            this.iteration.Element.Add(ed2);
            this.iteration.Option.Add(option);
            this.iteration.PossibleFiniteStateList.Add(pl);
            this.iteration.ActualFiniteStateList.Add(al);
            pl.PossibleState.Add(ps1);
            pl.PossibleState.Add(ps2);
            al.ActualState.Add(as1);
            al.ActualState.Add(as2);
            model.Iteration.Add(this.iteration);

            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(model.Iid, null), new Lazy<Thing>(() => model));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(this.iteration.Iid, null), new Lazy<Thing>(() => this.iteration));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(option.Iid, this.iteration.Iid), new Lazy<Thing>(() => option));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(pl.Iid, this.iteration.Iid), new Lazy<Thing>(() => pl));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(ps1.Iid, this.iteration.Iid), new Lazy<Thing>(() => ps1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(ps2.Iid, this.iteration.Iid), new Lazy<Thing>(() => ps2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(al.Iid, this.iteration.Iid), new Lazy<Thing>(() => al));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(as1.Iid, this.iteration.Iid), new Lazy<Thing>(() => as1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(as2.Iid, this.iteration.Iid), new Lazy<Thing>(() => as2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(ed1.Iid, this.iteration.Iid), new Lazy<Thing>(() => ed1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(ed2.Iid, this.iteration.Iid), new Lazy<Thing>(() => ed2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(us.Iid, this.iteration.Iid), new Lazy<Thing>(() => us));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(p1.Iid, this.iteration.Iid), new Lazy<Thing>(() => p1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(v1.Iid, this.iteration.Iid), new Lazy<Thing>(() => v1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(sub.Iid, this.iteration.Iid), new Lazy<Thing>(() => sub));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(vs.Iid, this.iteration.Iid), new Lazy<Thing>(() => vs));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(o1.Iid, this.iteration.Iid), new Lazy<Thing>(() => o1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(ov1.Iid, this.iteration.Iid), new Lazy<Thing>(() => ov1));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(p2.Iid, this.iteration.Iid), new Lazy<Thing>(() => p2));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(v21.Iid, this.iteration.Iid), new Lazy<Thing>(() => v21));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(v22.Iid, this.iteration.Iid), new Lazy<Thing>(() => v22));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(p3.Iid, this.iteration.Iid), new Lazy<Thing>(() => p3)); 
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(v3.Iid, this.iteration.Iid), new Lazy<Thing>(() => v3));
            assembler.Cache.TryAdd(new Tuple<Guid, Guid?>(scale.Iid, null), new Lazy<Thing>(() => scale));

            #endregion



            this.service = new DataWriteService(this.session.Object, this.permissionService.Object);
        }

        [Test]
        public void VerifyWritesManualWorkAsExpected()
        {
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain1, null)}
                });

            this.service.UpdateDomainManualValue(this.iteration, @"ed1.scalar2\opt\ps1", "5.369");
            this.session.Verify(x => x.Write(It.IsAny<OperationContainer>()), Times.Once);
        }

        [Test]
        public void VerifyWritesManualthrowsPermission()
        {
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain2, null)}
                });

            Assert.Throws<Cdp4ScriptException>(() => this.service.UpdateDomainManualValue(this.iteration, @"ed1.scalar2\opt\ps1", "5.369"));
        }

        [Test]
        public void VerifyWritesManualSubscriptionsWorks()
        {
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain2, null)}
                });

            this.service.UpdateDomainManualValue(this.iteration, @"ed2.scalar1", "5.369");
            this.session.Verify(x => x.Write(It.Is<OperationContainer>(oc => oc.Operations.Any(o => o.ModifiedThing is CDP4Common.DTO.ParameterSubscriptionValueSet))), Times.Once);
        }

        [Test]
        public void VerifyWriteReferenceOverridethrowsPermission()
        {
            this.session.Setup(x => x.OpenIterations).Returns(
                new Dictionary<Iteration, Tuple<DomainOfExpertise, Participant>>
                {
                    {this.iteration, new Tuple<DomainOfExpertise, Participant>(this.domain1, null)}
                });

            this.service.UpdateDomainComputedValue(this.iteration, @"ed1.usage.scalar1", "5.369");
            this.session.Verify(x => x.Write(It.Is<OperationContainer>(oc => oc.Operations.Any(o => o.ModifiedThing is CDP4Common.DTO.ParameterOverrideValueSet))), Times.Once);
        }
    }
}

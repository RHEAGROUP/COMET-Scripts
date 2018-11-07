// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Cdp4Service.cs" company="RHEA System S.A.">
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

namespace CDP4Scripts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using CDP4Common;
    using CDP4Common.CommonData;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;
    using CDP4Dal;
    using CDP4Dal.DAL;
    using CDP4ServicesDal;
    using CDP4WspDal;

    /// <summary>
    /// The entry-point of the library
    /// </summary>
    public class Cdp4Service
    {
        /// <summary>
        /// The current <see cref="ISession"/>
        /// </summary>
        private readonly ISession session;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cdp4Service"/>
        /// </summary>
        /// <param name="user">The current username</param>
        /// <param name="pwd">The password associated to the user</param>
        /// <param name="host">The complete hostname</param>
        /// <param name="proxyuser">The proxy user</param>
        /// <param name="proxypass">the proxy password</param>
        /// <param name="proxyuri">The proxy uri</param>
        /// <param name="serviceType">The web-service type, default to "CDP4"</param>
        public Cdp4Service(string user, string pwd, string host, string proxyuser = "", string proxypass = "", string proxyuri = "", string serviceType = "CDP4")
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                throw new Cdp4ScriptException("The user cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new Cdp4ScriptException("The user cannot be null or whitespace");
            }

            var proxySettings = !string.IsNullOrWhiteSpace(proxyuri) ? new ProxySettings(new Uri(proxyuri), proxyuser, proxypass) : null;

            if (serviceType.Equals(Constants.CDP4_SERVICE_TYPE, StringComparison.InvariantCultureIgnoreCase))
            {
                this.session = new Session(new CdpServicesDal(), new Credentials(user, pwd, new Uri(host), proxySettings));
            }
            else if (serviceType.Equals(Constants.WSP_SERVICE_TYPE, StringComparison.InvariantCultureIgnoreCase))
            {
                this.session = new Session(new WspDal(), new Credentials(user, pwd, new Uri(host), proxySettings));
            }

            this.Read = new DataReadService(this.session);
        }

        /// <summary>
        /// Gets the <see cref="DataReadService"/>
        /// </summary>
        public DataReadService Read { get; }

        /// <summary>
        /// Refreshes the specified data
        /// </summary>
        public void Refresh()
        {
            this.session.Refresh().Wait();
        }

        /// <summary>
        /// Reloads the data
        /// </summary>
        public void Reload()
        {
            this.session.Reload().Wait();
        }

        /// <summary>
        /// Closes the current session
        /// </summary>
        public void CloseSession()
        {
            this.session.Close().Wait();
        }

        /// <summary>
        /// Load and return an <see cref="Iteration"/>
        /// </summary>
        /// <param name="modelShortName">The short-name of the engineering-model to load</param>
        /// <param name="iterationnumber">the iteration-number</param>
        /// <param name="domainShortName">The short-name of the domain of the <see cref="Participant"/></param>
        /// <returns>The Iteration</returns>
        public Iteration LoadIteration(string modelShortName, int iterationnumber, string domainShortName)
        {
            this.session.Open().Wait();

            var iterationsetup = this.session.Assembler.Cache.Select(x => x.Value.Value).OfType<IterationSetup>().
                FirstOrDefault(x => ((EngineeringModelSetup) x.Container).ShortName == modelShortName && x.IterationNumber == iterationnumber);

            if (iterationsetup == null)
            {
                throw new Cdp4ScriptException("The iteration-setup was not found");
            }

            var domain = this.session.Assembler.Cache.Select(x => x.Value.Value).OfType<DomainOfExpertise>().
                FirstOrDefault(x => x.ShortName == domainShortName);

            if (domain == null)
            {
                throw new Cdp4ScriptException("The domain was not found");
            }

            // check participant domain validity
            var modelsetup = (EngineeringModelSetup)iterationsetup.Container;
            if (!modelsetup.Participant.Any(x => x.Person.Iid == this.session.ActivePerson.Iid && x.Domain.Any(d => d.Iid == domain.Iid)))
            {
                throw new Cdp4ScriptException("The participant for the current person with the specified domain was not found");
            }

            var model = new EngineeringModel(modelsetup.EngineeringModelIid, null, new Uri(this.session.DataSourceUri));
            var iteration = new Iteration(iterationsetup.IterationIid, null, new Uri(this.session.DataSourceUri));
            model.Iteration.Add(iteration);

            this.session.Read(iteration, domain).Wait();

            iteration = this.session.OpenIterations.Keys.FirstOrDefault(x => x.Iid == iterationsetup.IterationIid);
            if (iteration == null)
            {
                throw new Cdp4ScriptException("The iteration couldnt be open");
            }

            return iteration;
        }

        /// <summary>
        /// Unload the specified <paramref name="iteration"/>
        /// </summary>
        /// <param name="iteration">The <see cref="Iteration"/> to close</param>
        public void UnloadIteration(Iteration iteration)
        {
            this.session.CloseIterationSetup(iteration.IterationSetup);
        }
    }
}

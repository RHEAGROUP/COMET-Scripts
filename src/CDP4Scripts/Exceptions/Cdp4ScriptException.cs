// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Cdp4ScriptException.cs" company="RHEA System S.A.">
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

    /// <summary>
    /// An exception class thrown by the library
    /// </summary>
    public class Cdp4ScriptException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cdp4ScriptException"/>
        /// </summary>
        /// <param name="message">The message</param>
        public Cdp4ScriptException(string message) : base(message)
        {
        }
    }
}

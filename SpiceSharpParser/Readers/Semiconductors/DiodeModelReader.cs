﻿using System.Collections.Generic;
using SpiceSharp.Components;

namespace SpiceSharp.Parser.Readers
{
    /// <summary>
    /// A class that can read diode models
    /// </summary>
    public class DiodeModelReader : Reader
    {
        /// <summary>
        /// Read
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="parameters">Parameters</param>
        /// <param name="netlist">Netlist</param>
        /// <returns></returns>
        public override bool Read(Token name, List<object> parameters, Netlist netlist)
        {
            DiodeModel model = new DiodeModel(ReadIdentifier(name));
            ReadParameters(model, parameters);
            netlist.Circuit.Components.Add(model);
            return true;
        }
    }
}
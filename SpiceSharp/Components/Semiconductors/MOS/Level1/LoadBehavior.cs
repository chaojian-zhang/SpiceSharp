﻿using System;
using SpiceSharp.Algebra;
using SpiceSharp.Attributes;
using SpiceSharp.Behaviors;
using SpiceSharp.Circuits;
using SpiceSharp.Diagnostics;
using SpiceSharp.Simulations;

namespace SpiceSharp.Components.MosfetBehaviors.Level1
{
    /// <summary>
    /// General behavior for a <see cref="Mosfet1"/>
    /// </summary>
    public class LoadBehavior : Behaviors.LoadBehavior, IConnectedBehavior
    {
        /// <summary>
        /// Necessary behaviors
        /// </summary>
        private BaseParameters _bp;
        private TemperatureBehavior _temp;
        private ModelBaseParameters _mbp;

        /// <summary>
        /// Nodes
        /// </summary>
        private int _drainNode, _gateNode, _sourceNode, _bulkNode;
        public int DrainNodePrime { get; protected set; }
        public int SourceNodePrime { get; protected set; }

        [PropertyName("von"), PropertyInfo(" ")]
        public double Von { get; protected set; }
        [PropertyName("vdsat"), PropertyInfo("Saturation drain voltage")]
        public double SaturationVoltageDs { get; protected set; }
        [PropertyName("id"), PropertyInfo("Drain current")]
        public double DrainCurrent { get; protected set; }
        [PropertyName("ibs"), PropertyInfo("B-S junction current")]
        public double BsCurrent { get; protected set; }
        [PropertyName("ibd"), PropertyInfo("B-D junction current")]
        public double BdCurrent { get; protected set; }
        [PropertyName("gmb"), PropertyName("gmbs"), PropertyInfo("Bulk-Source transconductance")]
        public double TransconductanceBs { get; protected set; }
        [PropertyName("gm"), PropertyInfo("Transconductance")]
        public double Transconductance { get; protected set; }
        [PropertyName("gds"), PropertyInfo("Drain-Source conductance")]
        public double CondDs { get; protected set; }
        [PropertyName("gbd"), PropertyInfo("Bulk-Drain conductance")]
        public double CondBd { get; protected set; }
        [PropertyName("gbs"), PropertyInfo("Bulk-Source conductance")]
        public double CondBs { get; protected set; }

        /// <summary>
        /// Extra variables
        /// </summary>
        public double Mode { get; protected set; }

        public double VoltageBd { get; protected set; }
        public double VoltageBs { get; protected set; }
        public double VoltageGs { get; protected set; }
        public double VoltageDs { get; protected set; }

        /// <summary>
        /// Matrix elements
        /// </summary>
        protected MatrixElement<double> DrainDrainPtr { get; private set; }
        protected MatrixElement<double> GateGatePtr { get; private set; }
        protected MatrixElement<double> SourceSourcePtr { get; private set; }
        protected MatrixElement<double> BulkBulkPtr { get; private set; }
        protected MatrixElement<double> DrainPrimeDrainPrimePtr { get; private set; }
        protected MatrixElement<double> SourcePrimeSourcePrimePtr { get; private set; }
        protected MatrixElement<double> DrainDrainPrimePtr { get; private set; }
        protected MatrixElement<double> GateBulkPtr { get; private set; }
        protected MatrixElement<double> GateDrainPrimePtr { get; private set; }
        protected MatrixElement<double> GateSourcePrimePtr { get; private set; }
        protected MatrixElement<double> SourceSourcePrimePtr { get; private set; }
        protected MatrixElement<double> BulkDrainPrimePtr { get; private set; }
        protected MatrixElement<double> BulkSourcePrimePtr { get; private set; }
        protected MatrixElement<double> DrainPrimeSourcePrimePtr { get; private set; }
        protected MatrixElement<double> DrainPrimeDrainPtr { get; private set; }
        protected MatrixElement<double> BulkGatePtr { get; private set; }
        protected MatrixElement<double> DrainPrimeGatePtr { get; private set; }
        protected MatrixElement<double> SourcePrimeGatePtr { get; private set; }
        protected MatrixElement<double> SourcePrimeSourcePtr { get; private set; }
        protected MatrixElement<double> DrainPrimeBulkPtr { get; private set; }
        protected MatrixElement<double> SourcePrimeBulkPtr { get; private set; }
        protected MatrixElement<double> SourcePrimeDrainPrimePtr { get; private set; }
        protected VectorElement<double> BulkPtr { get; private set; }
        protected VectorElement<double> DrainPrimePtr { get; private set; }
        protected VectorElement<double> SourcePrimePtr { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name</param>
        public LoadBehavior(Identifier name) : base(name) { }

        /// <summary>
        /// Setup behavior
        /// </summary>
        /// <param name="provider">Data provider</param>
        public override void Setup(SetupDataProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            // Get parameters
            _bp = provider.GetParameterSet<BaseParameters>("entity");
            _mbp = provider.GetParameterSet<ModelBaseParameters>("model");

            // Get behaviors
            _temp = provider.GetBehavior<TemperatureBehavior>("entity");
        }

        /// <summary>
        /// Connect
        /// </summary>
        /// <param name="pins">Pins</param>
        public void Connect(params int[] pins)
        {
            if (pins == null)
                throw new ArgumentNullException(nameof(pins));
            if (pins.Length != 4)
                throw new CircuitException("Pin count mismatch: 4 pins expected, {0} given".FormatString(pins.Length));
            _drainNode = pins[0];
            _gateNode = pins[1];
            _sourceNode = pins[2];
            _bulkNode = pins[3];
        }

        /// <summary>
        /// Gets matrix pointers
        /// </summary>
        /// <param name="nodes">Nodes</param>
        /// <param name="solver">Solver</param>
        public override void GetEquationPointers(Nodes nodes, Solver<double> solver)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (solver == null)
                throw new ArgumentNullException(nameof(solver));

            // Add series drain node if necessary
            if (_mbp.DrainResistance > 0 || _mbp.SheetResistance > 0 && _bp.DrainSquares > 0)
                DrainNodePrime = nodes.Create(Name.Grow("#drain")).Index;
            else
                DrainNodePrime = _drainNode;

            // Add series source node if necessary
            if (_mbp.SourceResistance > 0 || _mbp.SheetResistance > 0 && _bp.SourceSquares > 0)
                SourceNodePrime = nodes.Create(Name.Grow("#source")).Index;
            else
                SourceNodePrime = _sourceNode;

            // Get matrix pointers
            DrainDrainPtr = solver.GetMatrixElement(_drainNode, _drainNode);
            GateGatePtr = solver.GetMatrixElement(_gateNode, _gateNode);
            SourceSourcePtr = solver.GetMatrixElement(_sourceNode, _sourceNode);
            BulkBulkPtr = solver.GetMatrixElement(_bulkNode, _bulkNode);
            DrainPrimeDrainPrimePtr = solver.GetMatrixElement(DrainNodePrime, DrainNodePrime);
            SourcePrimeSourcePrimePtr = solver.GetMatrixElement(SourceNodePrime, SourceNodePrime);
            DrainDrainPrimePtr = solver.GetMatrixElement(_drainNode, DrainNodePrime);
            GateBulkPtr = solver.GetMatrixElement(_gateNode, _bulkNode);
            GateDrainPrimePtr = solver.GetMatrixElement(_gateNode, DrainNodePrime);
            GateSourcePrimePtr = solver.GetMatrixElement(_gateNode, SourceNodePrime);
            SourceSourcePrimePtr = solver.GetMatrixElement(_sourceNode, SourceNodePrime);
            BulkDrainPrimePtr = solver.GetMatrixElement(_bulkNode, DrainNodePrime);
            BulkSourcePrimePtr = solver.GetMatrixElement(_bulkNode, SourceNodePrime);
            DrainPrimeSourcePrimePtr = solver.GetMatrixElement(DrainNodePrime, SourceNodePrime);
            DrainPrimeDrainPtr = solver.GetMatrixElement(DrainNodePrime, _drainNode);
            BulkGatePtr = solver.GetMatrixElement(_bulkNode, _gateNode);
            DrainPrimeGatePtr = solver.GetMatrixElement(DrainNodePrime, _gateNode);
            SourcePrimeGatePtr = solver.GetMatrixElement(SourceNodePrime, _gateNode);
            SourcePrimeSourcePtr = solver.GetMatrixElement(SourceNodePrime, _sourceNode);
            DrainPrimeBulkPtr = solver.GetMatrixElement(DrainNodePrime, _bulkNode);
            SourcePrimeBulkPtr = solver.GetMatrixElement(SourceNodePrime, _bulkNode);
            SourcePrimeDrainPrimePtr = solver.GetMatrixElement(SourceNodePrime, DrainNodePrime);

            // Get rhs pointers
            BulkPtr = solver.GetRhsElement(_bulkNode);
            DrainPrimePtr = solver.GetRhsElement(DrainNodePrime);
            SourcePrimePtr = solver.GetRhsElement(SourceNodePrime);
        }

        /// <summary>
        /// Unsetup
        /// </summary>
        public override void Unsetup()
        {
            // Remove references
            DrainDrainPtr = null;
            GateGatePtr = null;
            SourceSourcePtr = null;
            BulkBulkPtr = null;
            DrainPrimeDrainPrimePtr = null;
            SourcePrimeSourcePrimePtr = null;
            DrainDrainPrimePtr = null;
            GateBulkPtr = null;
            GateDrainPrimePtr = null;
            GateSourcePrimePtr = null;
            SourceSourcePrimePtr = null;
            BulkDrainPrimePtr = null;
            BulkSourcePrimePtr = null;
            DrainPrimeSourcePrimePtr = null;
            DrainPrimeDrainPtr = null;
            BulkGatePtr = null;
            DrainPrimeGatePtr = null;
            SourcePrimeGatePtr = null;
            SourcePrimeSourcePtr = null;
            DrainPrimeBulkPtr = null;
            SourcePrimeBulkPtr = null;
            SourcePrimeDrainPrimePtr = null;
        }

        /// <summary>
        /// Execute behavior
        /// </summary>
        /// <param name="simulation">Base simulation</param>
        public override void Load(BaseSimulation simulation)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));

            var state = simulation.RealState;
            double vt, effectiveLength, drainSatCur, sourceSatCur, beta,
                vgs, vds, vbs, vbd, vgd, vgdo, von, evbs, evbd, ceqbs, ceqbd,
                vdsat, cdrain, cdreq;
            int check, xnrm, xrev;

            vt = Circuit.KOverQ * _bp.Temperature;
            check = 1;

            /* DETAILPROF */

            /* first, we compute a few useful values - these could be
			 * pre - computed, but for historical reasons are still done
			 * here.  They may be moved at the expense of instance size
			 */
            effectiveLength = _bp.Length - 2 * _mbp.LateralDiffusion;
            
            // This is how Spice 3f5 implements it... There may be better ways to check for 0.0
            if (_temp.TempSaturationCurrentDensity.Equals(0) || _bp.DrainArea.Value.Equals(0) || _bp.SourceArea.Value.Equals(0))
            {
                drainSatCur = _temp.TempSaturationCurrent;
                sourceSatCur = _temp.TempSaturationCurrent;
            }
            else
            {
                drainSatCur = _temp.TempSaturationCurrentDensity * _bp.DrainArea;
                sourceSatCur = _temp.TempSaturationCurrentDensity * _bp.SourceArea;
            }

            beta = _temp.TempTransconductance * _bp.Width / effectiveLength;
            /* 
			 * ok - now to do the start - up operations
			 * 
			 * we must get values for vbs, vds, and vgs from somewhere
			 * so we either predict them or recover them from last iteration
			 * These are the two most common cases - either a prediction
			 * step or the general iteration step and they
			 * share some code, so we put them first - others later on
			 */
            if (state.Init == RealState.InitializationStates.InitFloat || state.Init == RealState.InitializationStates.InitTransient ||
                state.Init == RealState.InitializationStates.InitFix && !_bp.Off)
            {
                // general iteration
                vbs = _mbp.MosfetType * (state.Solution[_bulkNode] - state.Solution[SourceNodePrime]);
                vgs = _mbp.MosfetType * (state.Solution[_gateNode] - state.Solution[SourceNodePrime]);
                vds = _mbp.MosfetType * (state.Solution[DrainNodePrime] - state.Solution[SourceNodePrime]);

                /* now some common crunching for some more useful quantities */
                vbd = vbs - vds;
                vgd = vgs - vds;
                vgdo = VoltageGs - VoltageDs;
                von = _mbp.MosfetType * Von;

                /* 
				 * limiting
				 * we want to keep device voltages from changing
				 * so fast that the exponentials churn out overflows
				 * and similar rudeness
				 */

                if (VoltageDs >= 0)
                {
                    vgs = Transistor.LimitFet(vgs, VoltageGs, von);
                    vds = vgs - vgd;
                    vds = Transistor.LimitVoltageDs(vds, VoltageDs);
                }
                else
                {
                    vgd = Transistor.LimitFet(vgd, vgdo, von);
                    vds = vgs - vgd;
                    vds = -Transistor.LimitVoltageDs(-vds, -VoltageDs);
                    vgs = vgd + vds;
                }
                if (vds >= 0)
                {
                    vbs = Transistor.LimitJunction(vbs, VoltageBs, vt, _temp.SourceVCritical, out check);
                }
                else
                {
                    vbd = Transistor.LimitJunction(vbd, VoltageBd, vt, _temp.DrainVCritical, out check);
                    vbs = vbd + vds;
                }
            }
            else
            {
                /* ok - not one of the simple cases, so we have to
				 * look at all of the possibilities for why we were
				 * called.  We still just initialize the three voltages
				 */

                if (state.Init == RealState.InitializationStates.InitJunction && !_bp.Off)
                {
                    vds = _mbp.MosfetType * _bp.InitialVoltageDs;
                    vgs = _mbp.MosfetType * _bp.InitialVoltageGs;
                    vbs = _mbp.MosfetType * _bp.InitialVoltageBs;

                    // This is what Spice 3f5 does, but I'm not sure how valid this still is
                    if (vds.Equals(0) && vgs.Equals(0) && vbs.Equals(0) && (state.UseDc || state.Domain == RealState.DomainType.None || !state.UseIc))
                    {
                        vbs = -1;
                        vgs = _mbp.MosfetType * _temp.TempVt0;
                        vds = 0;
                    }
                }
                else
                {
                    vbs = vgs = vds = 0;
                }
            }

            /* DETAILPROF */

            /* 
			 * now all the preliminaries are over - we can start doing the
			 * real work
			 */
            vbd = vbs - vds;
            vgd = vgs - vds;

            /* 
			 * bulk - source and bulk - drain diodes
			 * here we just evaluate the ideal diode current and the
			 * corresponding derivative (conductance).
			 */
            if (vbs <= 0)
            {
                CondBs = sourceSatCur / vt;
                BsCurrent = CondBs * vbs;
                CondBs += state.Gmin;
            }
            else
            {
                evbs = Math.Exp(Math.Min(Transistor.MaximumExponentArgument, vbs / vt));
                CondBs = sourceSatCur * evbs / vt + state.Gmin;
                BsCurrent = sourceSatCur * (evbs - 1);
            }
            if (vbd <= 0)
            {
                CondBd = drainSatCur / vt;
                BdCurrent = CondBd * vbd;
                CondBd += state.Gmin;
            }
            else
            {
                evbd = Math.Exp(Math.Min(Transistor.MaximumExponentArgument, vbd / vt));
                CondBd = drainSatCur * evbd / vt + state.Gmin;
                BdCurrent = drainSatCur * (evbd - 1);
            }

            /* now to determine whether the user was able to correctly
			 * identify the source and drain of his device
			 */
            if (vds >= 0)
            {
                /* normal mode */
                Mode = 1;
            }
            else
            {
                /* inverse mode */
                Mode = -1;
            }

            /* DETAILPROF */
            {
                /* 
				 * this block of code evaluates the drain current and its 
				 * derivatives using the shichman - hodges model and the 
				 * charges associated with the gate, channel and bulk for 
				 * mosfets
				 */

                /* the following 4 variables are local to this code block until 
				 * it is obvious that they can be made global 
				 */
                double arg;
                double betap;
                double sarg;
                double vgst;

                if ((Mode > 0 ? vbs : vbd) <= 0)
                {
                    sarg = Math.Sqrt(_temp.TempPhi - (Mode > 0 ? vbs : vbd));
                }
                else
                {
                    sarg = Math.Sqrt(_temp.TempPhi);
                    sarg = sarg - (Mode > 0 ? vbs : vbd) / (sarg + sarg);
                    sarg = Math.Max(0, sarg);
                }
                von = _temp.TempVoltageBi * _mbp.MosfetType + _mbp.Gamma * sarg;
                vgst = (Mode > 0 ? vgs : vgd) - von;
                vdsat = Math.Max(vgst, 0);
                if (sarg <= 0)
                {
                    arg = 0;
                }
                else
                {
                    arg = _mbp.Gamma / (sarg + sarg);
                }
                if (vgst <= 0)
                {
                    /* 
					 * cutoff region
					 */
                    cdrain = 0;
                    Transconductance = 0;
                    CondDs = 0;
                    TransconductanceBs = 0;
                }
                else
                {
                    /* 
					 * saturation region
					 */
                    betap = beta * (1 + _mbp.Lambda * (vds * Mode));
                    if (vgst <= vds * Mode)
                    {
                        cdrain = betap * vgst * vgst * .5;
                        Transconductance = betap * vgst;
                        CondDs = _mbp.Lambda * beta * vgst * vgst * .5;
                        TransconductanceBs = Transconductance * arg;
                    }
                    else
                    {
                        /* 
						* linear region
						*/
                        cdrain = betap * (vds * Mode) * (vgst - .5 * (vds * Mode));
                        Transconductance = betap * (vds * Mode);
                        CondDs = betap * (vgst - vds * Mode) + _mbp.Lambda * beta * (vds * Mode) * (vgst - .5 * (vds * Mode));
                        TransconductanceBs = Transconductance * arg;
                    }
                }
                /* 
				 * finished
				 */
            }

            /* now deal with n vs p polarity */
            Von = _mbp.MosfetType * von;
            SaturationVoltageDs = _mbp.MosfetType * vdsat;
            /* line 490 */
            /* 
			 * COMPUTE EQUIVALENT DRAIN CURRENT SOURCE
			 */
            DrainCurrent = Mode * cdrain - BdCurrent;

            /* 
			 * check convergence
			 */
            if (!_bp.Off || !(state.Init == RealState.InitializationStates.InitFix))
            {
                if (check == 1)
                    state.IsConvergent = false;
            }

            /* DETAILPROF */

            /* save things away for next time */
            VoltageBs = vbs;
            VoltageBd = vbd;
            VoltageGs = vgs;
            VoltageDs = vds;

            /* 
			 * load current vector
			 */
            ceqbs = _mbp.MosfetType * (BsCurrent - (CondBs - state.Gmin) * vbs);
            ceqbd = _mbp.MosfetType * (BdCurrent - (CondBd - state.Gmin) * vbd);
            if (Mode >= 0)
            {
                xnrm = 1;
                xrev = 0;
                cdreq = _mbp.MosfetType * (cdrain - CondDs * vds - Transconductance * vgs - TransconductanceBs * vbs);
            }
            else
            {
                xnrm = 0;
                xrev = 1;
                cdreq = -_mbp.MosfetType * (cdrain - CondDs * -vds - Transconductance * vgd - TransconductanceBs * vbd);
            }
            BulkPtr.Value -= ceqbs + ceqbd;
            DrainPrimePtr.Value += ceqbd - cdreq;
            SourcePrimePtr.Value += cdreq + ceqbs;

            /* 
			 * load y matrix
			 */
            DrainDrainPtr.Value += _temp.DrainConductance;
            SourceSourcePtr.Value += _temp.SourceConductance;
            BulkBulkPtr.Value += CondBd + CondBs;
            DrainPrimeDrainPrimePtr.Value += _temp.DrainConductance + CondDs + CondBd + xrev * (Transconductance + TransconductanceBs);
            SourcePrimeSourcePrimePtr.Value += _temp.SourceConductance + CondDs + CondBs + xnrm * (Transconductance + TransconductanceBs);
            DrainDrainPrimePtr.Value += -_temp.DrainConductance;
            SourceSourcePrimePtr.Value += -_temp.SourceConductance;
            BulkDrainPrimePtr.Value -= CondBd;
            BulkSourcePrimePtr.Value -= CondBs;
            DrainPrimeDrainPtr.Value += -_temp.DrainConductance;
            DrainPrimeGatePtr.Value += (xnrm - xrev) * Transconductance;
            DrainPrimeBulkPtr.Value += -CondBd + (xnrm - xrev) * TransconductanceBs;
            DrainPrimeSourcePrimePtr.Value += -CondDs - xnrm * (Transconductance + TransconductanceBs);
            SourcePrimeGatePtr.Value += -(xnrm - xrev) * Transconductance;
            SourcePrimeSourcePtr.Value += -_temp.SourceConductance;
            SourcePrimeBulkPtr.Value += -CondBs - (xnrm - xrev) * TransconductanceBs;
            SourcePrimeDrainPrimePtr.Value += -CondDs - xrev * (Transconductance + TransconductanceBs);
        }

        /// <summary>
        /// Test convergence
        /// </summary>
        /// <param name="simulation">Base simulation</param>
        /// <returns></returns>
        public override bool IsConvergent(BaseSimulation simulation)
        {
			if (simulation == null)
				throw new ArgumentNullException(nameof(simulation));

            var state = simulation.RealState;
            var config = simulation.BaseConfiguration;

            double vbs, vgs, vds, vbd, vgd, vgdo, delvbs, delvbd, delvgs, delvds, delvgd, cdhat, cbhat;

            vbs = _mbp.MosfetType * (state.Solution[_bulkNode] - state.Solution[SourceNodePrime]);
            vgs = _mbp.MosfetType * (state.Solution[_gateNode] - state.Solution[SourceNodePrime]);
            vds = _mbp.MosfetType * (state.Solution[DrainNodePrime] - state.Solution[SourceNodePrime]);
            vbd = vbs - vds;
            vgd = vgs - vds;
            vgdo = VoltageGs - VoltageDs;
            delvbs = vbs - VoltageBs;
            delvbd = vbd - VoltageBd;
            delvgs = vgs - VoltageGs;
            delvds = vds - VoltageDs;
            delvgd = vgd - vgdo;

            // these are needed for convergence testing
            // NOTE: Cd does not include contributions for transient simulations... Should check for a way to include them!
            if (Mode >= 0)
            {
                cdhat = DrainCurrent - CondBd * delvbd + TransconductanceBs * delvbs +
                    Transconductance * delvgs + CondDs * delvds;
            }
            else
            {
                cdhat = DrainCurrent - (CondBd - TransconductanceBs) * delvbd -
                    Transconductance * delvgd + CondDs * delvds;
            }
            cbhat = BsCurrent + BdCurrent + CondBd * delvbd + CondBs * delvbs;

            /*
             *  check convergence
             */
            // NOTE: relative and absolute tolerances need to be gotten from the configuration, temporarely set to constants here
            double tol = config.RelativeTolerance * Math.Max(Math.Abs(cdhat), Math.Abs(DrainCurrent)) + config.AbsoluteTolerance;
            if (Math.Abs(cdhat - DrainCurrent) >= tol)
            {
                state.IsConvergent = false;
                return false;
            }

            tol = config.RelativeTolerance * Math.Max(Math.Abs(cbhat), Math.Abs(BsCurrent + BdCurrent)) + config.AbsoluteTolerance;
            if (Math.Abs(cbhat - (BsCurrent + BdCurrent)) > tol)
            {
                state.IsConvergent = false;
                return false;
            }
            return true;
        }
    }
}

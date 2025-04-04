// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;
using ILLink.RoslynAnalyzer.DataFlow;
using ILLink.Shared.DataFlow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace ILLink.RoslynAnalyzer.TrimAnalysis
{
	public class TrimDataFlowAnalysis
		: ForwardDataFlowAnalysis<
			LocalState<ValueSet<SingleValue>>,
			LocalDataFlowState<ValueSet<SingleValue>, ValueSetLattice<SingleValue>>,
			LocalStateLattice<ValueSet<SingleValue>, ValueSetLattice<SingleValue>>,
			BlockProxy,
			RegionProxy,
			ControlFlowGraphProxy,
			TrimAnalysisVisitor
		>
	{
		readonly ControlFlowGraphProxy ControlFlowGraph;

		readonly LocalStateLattice<ValueSet<SingleValue>, ValueSetLattice<SingleValue>> Lattice;

		readonly OperationBlockAnalysisContext Context;

		public TrimDataFlowAnalysis (OperationBlockAnalysisContext context, ControlFlowGraph cfg)
		{
			ControlFlowGraph = new ControlFlowGraphProxy (cfg);
			Lattice = new (new ValueSetLattice<SingleValue> ());
			Context = context;
		}

		public TrimAnalysisPatternStore ComputeTrimAnalysisPatterns ()
		{
			var lValueFlowCaptures = LValueFlowCapturesProvider.CreateLValueFlowCaptures (ControlFlowGraph.ControlFlowGraph);
			var visitor = new TrimAnalysisVisitor (Lattice, Context, lValueFlowCaptures);
			Fixpoint (ControlFlowGraph, Lattice, visitor);
			return visitor.TrimAnalysisPatterns;
		}

#if DEBUG
#pragma warning disable CA1805 // Do not initialize unnecessarily
		// Set this to a method name to trace the analysis of the method.
		readonly string? traceMethod = null;

		bool trace = false;

		// Set this to true to print out the dataflow states encountered during the analysis.
		readonly bool showStates = false;

		static readonly TracingType tracingMechanism = Debugger.IsAttached ? TracingType.Debug : TracingType.Console;
#pragma warning restore CA1805 // Do not initialize unnecessarily
		ControlFlowGraphProxy cfg;

		private enum TracingType
		{
			Console,
			Debug
		}

		public override void TraceStart (ControlFlowGraphProxy cfg)
		{
			this.cfg = cfg;
			var blocks = cfg.Blocks.ToList ();
			string? methodName = null;
			foreach (var block in blocks) {
				if (block.Block.Operations.FirstOrDefault () is not IOperation op)
					continue;

				var method = op.Syntax.FirstAncestorOrSelf<MethodDeclarationSyntax> ();
				if (method is MethodDeclarationSyntax)
					methodName = method.Identifier.ValueText;

				break;
			}

			if (methodName?.Equals (traceMethod) == true)
				trace = true;
		}

		public override void TraceVisitBlock (BlockProxy block)
		{
			if (!trace)
				return;

			TraceWrite ("block " + block.Block.Ordinal + ": ");
			if (block.Block.Operations.FirstOrDefault () is IOperation firstBlockOp) {
				TraceWriteLine (firstBlockOp.Syntax.ToString ());
			} else if (block.Block.BranchValue is IOperation branchOp) {
				TraceWriteLine (branchOp.Syntax.ToString ());
			} else {
				TraceWriteLine ("");
			}
			TraceWrite ("predecessors: ");
			foreach (var predecessor in cfg.GetPredecessors (block)) {
				var predProxy = predecessor.Block;
				TraceWrite (predProxy.Block.Ordinal + " ");
			}
			TraceWriteLine ("");
		}

		private static void TraceWriteLine (string tracingInfo)
		{
			switch (tracingMechanism) {
			case TracingType.Console:
				Console.WriteLine (tracingInfo);
				break;
			case TracingType.Debug:
				Debug.WriteLine (tracingInfo);
				break;
			default:
				throw new NotImplementedException (message: "invalid TracingType is being used");
			}
		}

		private static void TraceWrite (string tracingInfo)
		{
			switch (tracingMechanism) {
			case TracingType.Console:
				Console.Write (tracingInfo);
				break;
			case TracingType.Debug:
				Debug.Write (tracingInfo);
				break;
			default:
				throw new NotImplementedException (message: "invalid TracingType is being used");
			}
		}

		static void WriteIndented (string? s, int level)
		{
			string[]? lines = s?.Trim ().Split (new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			if (lines == null)
				return;
			foreach (var line in lines) {
				TraceWrite (new String ('\t', level));
				TraceWriteLine (line);
			}
		}

		public override void TraceBlockInput (
			LocalState<ValueSet<SingleValue>> normalState,
			LocalState<ValueSet<SingleValue>>? exceptionState,
			LocalState<ValueSet<SingleValue>>? exceptionFinallyState
		)
		{
			if (trace && showStates) {
				WriteIndented ("--- before transfer ---", 1);
				WriteIndented ("normal state:", 1);
				WriteIndented (normalState.ToString (), 2);
				WriteIndented ("exception state:", 1);
				WriteIndented (exceptionState?.ToString (), 2);
				WriteIndented ("finally exception state:", 1);
				WriteIndented (exceptionFinallyState?.ToString (), 2);
			}
		}

		public override void TraceBlockOutput (
			LocalState<ValueSet<SingleValue>> normalState,
			LocalState<ValueSet<SingleValue>>? exceptionState,
			LocalState<ValueSet<SingleValue>>? exceptionFinallyState
		)
		{
			if (trace && showStates) {
				WriteIndented ("--- after transfer ---", 1);
				WriteIndented ("normal state:", 1);
				WriteIndented (normalState.ToString (), 2);
				WriteIndented ("exception state:", 1);
				WriteIndented (exceptionState?.ToString (), 2);
				WriteIndented ("finally state:", 1);
				WriteIndented (exceptionFinallyState?.ToString (), 2);
			}
		}
#endif
	}
}

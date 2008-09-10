//
// AvoidMessageChainsRule class
//
// Authors:
//	Néstor Salceda <nestor.salceda@gmail.com>
//	Sebastien Pouliot <sebastien@ximian.com>
//
// (C) 2008 Néstor Salceda
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Gendarme.Framework;
using Gendarme.Framework.Engines;
using Gendarme.Framework.Helpers;
using Gendarme.Framework.Rocks;

namespace Gendarme.Rules.Smells {

	[Problem ("The code contains long sequences of method calls or temporary variables, this means your code is hardly coupled to the navigation structure.")]
	[Solution ("You can apply the Hide Delegate refactoring or Extract Method to push down the chain.")]
	[EngineDependency (typeof (OpCodeEngine))]
	public class AvoidMessageChainsRule : Rule, IMethodRule {
		private int maxChainLength = 5;

		public int MaxChainLength {
			get {
				return maxChainLength;
			}
			set {
				maxChainLength = value;
			}
		}

		// this mask represents Callvirt, Call, Newobj and Newarr
		static OpCodeBitmask chain = new OpCodeBitmask (0x8000000000, 0x4400000000000, 0x400, 0x0);

		public RuleResult CheckMethod (MethodDefinition method)
		{
			if (!method.HasBody) 
				return RuleResult.DoesNotApply;

			// no chain are possible without Call[virt] instructions within the method
			if (!OpCodeBitmask.Calls.Intersect (OpCodeEngine.GetBitmask (method)))
				return RuleResult.DoesNotApply;

			// walk back so we don't process very long chains multiple times
			// (we don't need to go down to zero since it would not be big enough for a chain to exists)
			InstructionCollection ic = method.Body.Instructions;
			for (int i = ic.Count - 1; i >= MaxChainLength; i--) {
				Instruction ins = ic [i];
				// continue until we find a Call[virt] instruction
				if (!OpCodeBitmask.Calls.Get (ins.OpCode.Code))
					continue;

				// operators "breaks" chains
				MethodReference mr = (ins.Operand as MethodReference);
				if (mr.Name.StartsWith ("op_"))
					continue;

				int counter = 1;
				// trace back every call (including new objects and arrays) and
				// check if the caller is a call (i.e. not a local)
				Instruction caller = ins.TraceBack (method);
				while ((caller != null) && chain.Get (caller.OpCode.Code)) {
					counter++;
					i = ic.IndexOf (caller);
					caller = caller.TraceBack (method);
				}

				if (counter >= MaxChainLength) {
					string msg = String.Format ("Chain length {0} versus maximum of {1}.", counter, MaxChainLength);
					Runner.Report (method, ins, Severity.Medium, Confidence.Normal, msg);
				}
			}

			return Runner.CurrentRuleResult;
		}
	}
}

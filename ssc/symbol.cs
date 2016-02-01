﻿using arookas.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace arookas {
	class sunSymbolTable : IEnumerable<sunSymbol> {
		List<sunSymbol> mSymbols;

		public int Count { get { return mSymbols.Count; } }
		public int CallableCount { get { return Callables.Count(); } }
		public int BuiltinCount { get { return Builtins.Count(); } }
		public int FunctionCount { get { return Functions.Count(); } }
		public int StorableCount { get { return Storables.Count(); } }
		public int VariableCount { get { return Variables.Count(); } }
		public int ConstantCount { get { return Constants.Count(); } }

		public IEnumerable<sunCallableSymbol> Callables { get { return mSymbols.OfType<sunCallableSymbol>(); } }
		public IEnumerable<sunBuiltinSymbol> Builtins { get { return mSymbols.OfType<sunBuiltinSymbol>(); } }
		public IEnumerable<sunFunctionSymbol> Functions { get { return mSymbols.OfType<sunFunctionSymbol>(); } }
		public IEnumerable<sunStorableSymbol> Storables { get { return mSymbols.OfType<sunStorableSymbol>(); } }
		public IEnumerable<sunVariableSymbol> Variables { get { return mSymbols.OfType<sunVariableSymbol>(); } }
		public IEnumerable<sunConstantSymbol> Constants { get { return mSymbols.OfType<sunConstantSymbol>(); } }

		public sunSymbolTable() {
			mSymbols = new List<sunSymbol>(10);
		}

		public void Add(sunSymbol symbol) {
			if (symbol == null) {
				throw new ArgumentNullException("symbol");
			}
			mSymbols.Add(symbol);
		}
		public void Clear() {
			mSymbols.Clear();
		}

		public IEnumerator<sunSymbol> GetEnumerator() {
			return mSymbols.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}

	abstract class sunSymbol {
		string mName;
		sunSymbolModifiers mModifiers;

		public string Name {
			get { return mName; }
		}
		public sunSymbolModifiers Modifiers {
			get { return mModifiers; }
			set { mModifiers = value; }
		}

		// symbol table
		public abstract sunSymbolType Type { get; }
		public abstract uint Data { get; }

		protected sunSymbol(string name) {
			mName = name;
		}

		public abstract void Compile(sunCompiler compiler);

		public static sunSymbolModifiers GetModifiers(sunNode modifierlist) {
			if (modifierlist == null) {
				return sunSymbolModifiers.None;
			}
			var modifiers = sunSymbolModifiers.None;
			if (modifierlist.Any(i => i is sunConstKeyword)) {
				modifiers |= sunSymbolModifiers.Constant;
			}
			if (modifierlist.Any(i => i is sunLocalKeyword)) {
				modifiers |= sunSymbolModifiers.Local;
			}
			return modifiers;
		}
	}

	abstract class sunCallableSymbol : sunSymbol {
		sunParameterInfo mParameters;

		public sunParameterInfo Parameters {
			get { return mParameters; }
		}

		public abstract bool HasCallSites { get; }

		protected sunCallableSymbol(string name, sunParameterInfo parameterInfo)
			: base(name) {
			mParameters = parameterInfo;
		}

		public abstract void OpenCallSite(sunCompiler compiler, int argumentCount);
		public abstract void CloseCallSites(sunCompiler compiler);
	}

	class sunBuiltinSymbol : sunCallableSymbol {
		int mIndex;
		List<sunBuiltinCallSite> mCallSites;

		public int Index {
			get { return mIndex; }
			set { mIndex = value; }
		}

		public override bool HasCallSites {
			get { return mCallSites.Count > 0; }
		}

		// symbol table
		public override sunSymbolType Type { get { return sunSymbolType.Builtin; } }
		public override uint Data { get { return (uint)Index; } }

		public sunBuiltinSymbol(string name, int index)
			: this(name, null, index) { }
		public sunBuiltinSymbol(string name, sunParameterInfo parameters, int index)
			: base(name, parameters) {
			mIndex = index;
			mCallSites = new List<sunBuiltinCallSite>(10);
		}

		public override void Compile(sunCompiler compiler) {
			// don't compile builtins
		}
		public override void OpenCallSite(sunCompiler compiler, int argumentCount) {
			var callSite = new sunBuiltinCallSite(compiler.Binary.OpenPoint(), argumentCount);
			mCallSites.Add(callSite);
			compiler.Binary.WriteFUNC(0, 0); // dummy
		}
		public override void CloseCallSites(sunCompiler compiler) {
			compiler.Binary.Keep();
			foreach (var callSite in mCallSites) {
				compiler.Binary.Goto(callSite.Point);
				compiler.Binary.WriteFUNC(mIndex, callSite.ArgCount);
			}
			compiler.Binary.Back();
		}

		struct sunBuiltinCallSite {
			sunPoint mPoint;
			int mArgCount;

			public sunPoint Point {
				get { return mPoint; }
			}
			public int ArgCount {
				get { return mArgCount; }
			}

			public sunBuiltinCallSite(sunPoint point, int argCount) {
				mPoint = point;
				mArgCount = argCount;
			}
		}
	}

	class sunFunctionSymbol : sunCallableSymbol {
		uint mOffset;
		sunNode mBody;
		List<sunPoint> mCallSites;

		public uint Offset {
			get { return mOffset; }
		}

		public override bool HasCallSites {
			get { return mCallSites.Count > 0; }
		}

		// symbol table
		public override sunSymbolType Type {
			get { return sunSymbolType.Function; }
		}
		public override uint Data {
			get { return (uint)Offset; }
		}

		public sunFunctionSymbol(string name, sunParameterInfo parameters, sunNode body)
			: base(name, parameters) {
			if (body == null) {
				throw new ArgumentNullException("body");
			}
			mCallSites = new List<sunPoint>(5);
			mBody = body;
		}

		public override void Compile(sunCompiler compiler) {
			mOffset = compiler.Binary.Offset;
			compiler.Context.Scopes.Push(sunScopeType.Function);
			compiler.Context.Scopes.ResetLocalCount();
			foreach (var parameter in Parameters) {
				compiler.Context.Scopes.DeclareVariable(parameter); // since there is no AST node for these, they won't affect MaxLocalCount
			}
			compiler.Binary.WriteMKDS(1);
			compiler.Binary.WriteMKFR(mBody.MaxLocalCount);
			mBody.Compile(compiler);
			compiler.Binary.WriteRET0();
			compiler.Context.Scopes.Pop();
		}
		public override void OpenCallSite(sunCompiler compiler, int argumentCount) {
			var point = compiler.Binary.WriteCALL(argumentCount);
			mCallSites.Add(point);
		}
		public override void CloseCallSites(sunCompiler compiler) {
			foreach (var callSite in mCallSites) {
				compiler.Binary.ClosePoint(callSite, Offset);
			}
		}
	}

	class sunParameterInfo : IEnumerable<string> {
		string[] mParameters;
		bool mVariadic;

		public int Minimum {
			get { return mParameters.Length; }
		}
		public bool IsVariadic {
			get { return mVariadic; }
		}

		public sunParameterInfo(IEnumerable<sunIdentifier> parameters, bool variadic) {
			// validate parameter names
			var duplicate = parameters.FirstOrDefault(a => parameters.Count(b => a.Value == b.Value) > 1);
			if (duplicate != null) {
				throw new sunRedeclaredParameterException(duplicate);
			}
			mParameters = parameters.Select(i => i.Value).ToArray();
			mVariadic = variadic;
		}
		public sunParameterInfo(IEnumerable<string> parameters, bool variadic) {
			// validate parameter names
			mParameters = parameters.ToArray();
			mVariadic = variadic;
		}

		public bool ValidateArgumentCount(int count) {
			return mVariadic ? count >= Minimum : count == Minimum;
		}

		public IEnumerator<string> GetEnumerator() {
			return mParameters.GetArrayEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}

	abstract class sunStorableSymbol : sunSymbol {
		protected sunStorableSymbol(string name)
			: base(name) { }

		public override void Compile(sunCompiler compiler) {
			CompileGet(compiler);
		}
		public abstract void CompileGet(sunCompiler compiler);
		public abstract void CompileSet(sunCompiler compiler);
		public virtual void CompileInc(sunCompiler compiler) {
			CompileGet(compiler);
			compiler.Binary.WriteINT(1);
			compiler.Binary.WriteADD();
		}
		public virtual void CompileDec(sunCompiler compiler) {
			CompileGet(compiler);
			compiler.Binary.WriteINT(1);
			compiler.Binary.WriteSUB();
		}
	}

	class sunVariableSymbol : sunStorableSymbol {
		int mDisplay, mIndex;

		public int Display {
			get { return mDisplay; }
		}
		public int Index {
			get { return mIndex; }
		}

		// symbol table
		public override sunSymbolType Type {
			get { return sunSymbolType.Variable; }
		}
		public override uint Data {
			get { return (uint)Index; }
		}

		public sunVariableSymbol(string name, int display, int index)
			: base(name) {
			mDisplay = display;
			mIndex = index;
		}

		public override void CompileGet(sunCompiler compiler) {
			compiler.Binary.WriteVAR(mDisplay, mIndex);
		}
		public override void CompileSet(sunCompiler compiler) {
			compiler.Binary.WriteASS(mDisplay, mIndex);
		}
		public override void CompileInc(sunCompiler compiler) {
			compiler.Binary.WriteINC(mDisplay, mIndex);
		}
		public override void CompileDec(sunCompiler compiler) {
			compiler.Binary.WriteDEC(mDisplay, mIndex);
		}
	}

	class sunConstantSymbol : sunStorableSymbol {
		sunExpression mExpression;

		// symbol table
		public override sunSymbolType Type {
			get { return sunSymbolType.Constant; }
		}
		public override uint Data {
			get { return 0; }
		}

		public sunConstantSymbol(string name, sunExpression expression)
			: base(name) {
			if (expression == null) {
				throw new ArgumentNullException("expression");
			}
			mExpression = expression;
		}

		public override void CompileGet(sunCompiler compiler) {
			mExpression.Compile(compiler);
		}
		public override void CompileSet(sunCompiler compiler) {
			// checks against this have to be implemented at a higher level
			throw new InvalidOperationException();
		}
	}

	enum sunSymbolType {
		Builtin,
		Function,
		Variable,
		Constant,
	}

	[Flags]
	enum sunSymbolModifiers {
		None = 0,
		Constant = 1 << 0,
		Local = 1 << 1,
	}
}

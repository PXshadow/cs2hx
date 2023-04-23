﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

class HxTypeParam {
	string Name;

	public HxTypeParam(Type t) {
		Name = t.Name;
	}

	public void Print() {
		Console.WriteLine(Name);
	}
}

class HxField {
	public FieldInfo field;

	public HxField(FieldInfo f) {
		field = f;
	}

	public void Print() {
		Console.WriteLine(field.Name);
		Console.WriteLine(ReadDLL.FullName(field.FieldType));
	}
}

class HxProperty {
	PropertyInfo prop;

	public HxProperty(PropertyInfo p) {
		prop = p;
	}

	public void Print() {
		Console.WriteLine(prop.Name);
		Console.WriteLine(ReadDLL.FullName(prop.PropertyType));
		Console.WriteLine(prop.CanRead);
		Console.WriteLine(prop.CanWrite);
	}
}

class HxMethod {
	MethodInfo meth;

	public HxMethod(MethodInfo m) {
		meth = m;
	}

	public void Print() {
		Console.WriteLine(meth.Name);
		Console.WriteLine(ReadDLL.FullName(meth.ReturnType));
		Console.WriteLine(meth.GetParameters().Length);
		foreach(var p in meth.GetParameters()) {
			Console.WriteLine(p.Name);
			Console.WriteLine(ReadDLL.FullName(p.ParameterType));
			Console.WriteLine(p.DefaultValue != null ? "true" : "false");
		}
	}
}

class HxTypeDef {
	public List<HxTypeParam> Params = new List<HxTypeParam>();
	public string Namespace = "";
	public string Name = "";
	public bool IsInterface = false;
	public string SuperPath = "";
	public List<string> Interfaces = new List<string>();
	public List<HxField> Fields = new List<HxField>();
	public List<HxProperty> Props = new List<HxProperty>();
	public List<HxMethod> Methods = new List<HxMethod>();
	public string? Doc = null;

	public void Print() {
		Console.WriteLine(ReadDLL.ConvertGenericTick(Name));
		Console.WriteLine(IsInterface ? "true" : "false");
		Console.WriteLine("cs" + (Namespace.Length > 0 ? "." : "") + Namespace.ToLower());
		Console.WriteLine(SuperPath);
		Console.WriteLine(Interfaces.Count);
		foreach(var i in Interfaces) {
			Console.WriteLine(i);
		}

		Console.WriteLine(Params.Count);
		foreach(var p in Params) {
			p.Print();
		}

		Console.WriteLine(Fields.Count);
		foreach(var f in Fields) {
			f.Print();
		}

		Console.WriteLine(Props.Count);
		foreach(var p in Props) {
			p.Print();
		}

		Console.WriteLine(Methods.Count);
		foreach(var m in Methods) {
			m.Print();
		}
	}
}

class ReadDLL {
	public static string FullName(Type? t) {
		if(t == null)
			return "";

		if(t.IsGenericParameter || t.IsGenericMethodParameter || t.IsGenericTypeParameter) {
			return t.Name;
		}

		if(t.IsArray) {
			return "Array<" + FullName(t.GetElementType()) + ">";
		}

		switch(t.Namespace + "." + t.Name) {
			case "System.Void": return "Void";
			case "System.Int32": return "Int";
			case "System.Double": return "Float";
			case "System.Boolean": return "Bool";
		}

		var args = t.GetGenericArguments();
		var generics = new List<string>();
		foreach(var arg in args) {
			generics.Add(FullName(arg));
		}

		var tp = (args.Length > 0 ? ("<" + string.Join(", ", generics) + ">") : "");
		return "cs." + (t.Namespace != null ? (t.Namespace.ToLower() + ".") : "") + ConvertGenericTick(t.Name) + tp;
	}

	// Convert TYPENAME`123 -> TYPENAME_123
	public static string ConvertGenericTick(string Name) {
		Regex rx = new Regex(@"^(.*)`(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		var m = rx.Matches(Name);
		if(m.Count > 0) {
			return m[0].Groups[1].Value + "_" + m[0].Groups[2].Value;
		}
		return Name;
	}

	// Store references to all the assemblies that are loaded.
	static List<Assembly> Assemblies = new List<Assembly>();

	// Given a namespace and name, find a type.
	static Type? FindCsType(string HaxeNS, string HaxeName) {
		HaxeNS = HaxeNS.ToLower();
		var assemblies = AppDomain.CurrentDomain.GetAssemblies().Concat(Assemblies);
		foreach(var assembly in assemblies) {
			foreach(Type type in assembly.GetTypes()) {
				if((type.Namespace?.ToLower() ?? "") == HaxeNS && ConvertGenericTick(type.Name) == HaxeName) {
					return type;
				}
			}
		}
		return null;
	}

	// Note to future self:
	// - We need proper capitalization (OK: System.String vs WRONG: cs.system.String)
	// - Use "TypeName`X" to denote a type with X type arguments (OK: List`1 vs WRONG: List WRONG: List<T>)
	public static void Main(String[] args) {
		// Make sure we have enough arguments!!
		if(args.Length <= 0) {
			Console.WriteLine("<no type path provided>");
			return;
		}

		// The first argument is the type path we're looking for!
		var HaxePath = args[0];

		// Load all the assmblies provided!
		// dll_reader <type_path> <dll_1> <dll_2> ...
		for(int i = 1; i < args.Length; i++) {
			Assemblies.Add(Assembly.LoadFrom(args[i]));
		}

		// Separate the namespace part and the class part.
		// The Haxe namespaces must all be lowercase, so they are compared differently.
		var HaxePathMems = HaxePath.Split(".");
		var HaxeNS = string.Join(".", HaxePathMems[..^1]).ToLower();
		var HaxeName = HaxePathMems.Last();

		// Find the type
		var t = FindCsType(HaxeNS, HaxeName);
		if(t == null) {
			Console.WriteLine("<no type found>");
			return;
		}

		// Retrieve all the type information and store in HxTypeDef
		var def = new HxTypeDef();

		def.Namespace = t.Namespace ?? "";
		def.Name = t.Name;
		def.IsInterface = t.IsInterface;
		def.SuperPath = FullName(t.BaseType);
		foreach(var i in t.GetInterfaces()) {
			def.Interfaces.Add(FullName(i));
		}
		foreach(var arg in t.GetGenericArguments()) {
			def.Params.Add(new HxTypeParam(arg));
		}

		foreach(var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Fields.Add(new HxField(f));
		}
		foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Props.Add(new HxProperty(p));
		}
		foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Methods.Add(new HxMethod(m));
		}

		// Print
		def.Print();
	}
}

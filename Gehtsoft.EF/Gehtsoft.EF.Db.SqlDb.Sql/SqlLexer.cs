/*
 * WARNING: this file has been generated by
 * Hime Parser Generator 3.5.0.0
 */
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using Hime.Redist;
using Hime.Redist.Lexer;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
	/// <summary>
	/// Represents a lexer
	/// </summary>
	[GeneratedCodeAttribute("Hime.SDK", "3.5.0.0")]
	public class SqlLexer : ContextFreeLexer
	{
		/// <summary>
		/// The automaton for this lexer
		/// </summary>
		private static readonly Automaton commonAutomaton = Automaton.Find(typeof(SqlLexer), "SqlLexer.bin");
		/// <summary>
		/// Contains the constant IDs for the terminals for this lexer
		/// </summary>
		[GeneratedCodeAttribute("Hime.SDK", "3.5.0.0")]
		public class ID
		{
			/// <summary>
			/// The unique identifier for terminal NEW_LINE
			/// </summary>
			public const int TerminalNewLine = 0x0003;
			/// <summary>
			/// The unique identifier for terminal WHITE_SPACE
			/// </summary>
			public const int TerminalWhiteSpace = 0x0004;
			/// <summary>
			/// The unique identifier for terminal INTEGER
			/// </summary>
			public const int TerminalInteger = 0x0005;
			/// <summary>
			/// The unique identifier for terminal REAL
			/// </summary>
			public const int TerminalReal = 0x0006;
			/// <summary>
			/// The unique identifier for terminal STRINGDQ
			/// </summary>
			public const int TerminalStringdq = 0x0007;
			/// <summary>
			/// The unique identifier for terminal STRINGSQ
			/// </summary>
			public const int TerminalStringsq = 0x0008;
			/// <summary>
			/// The unique identifier for terminal COMMENT_LINE
			/// </summary>
			public const int TerminalCommentLine = 0x0009;
			/// <summary>
			/// The unique identifier for terminal IDENTIFIER
			/// </summary>
			public const int TerminalIdentifier = 0x000A;
			/// <summary>
			/// The unique identifier for terminal SEPARATOR
			/// </summary>
			public const int TerminalSeparator = 0x000B;
		}
		/// <summary>
		/// Contains the constant IDs for the contexts for this lexer
		/// </summary>
		[GeneratedCodeAttribute("Hime.SDK", "3.5.0.0")]
		public class Context
		{
			/// <summary>
			/// The unique identifier for the default context
			/// </summary>
			public const int Default = 0;
		}
		/// <summary>
		/// The collection of terminals matched by this lexer
		/// </summary>
		/// <remarks>
		/// The terminals are in an order consistent with the automaton,
		/// so that terminal indices in the automaton can be used to retrieve the terminals in this table
		/// </remarks>
		private static readonly Symbol[] terminals = {
			new Symbol(0x0001, "ε"),
			new Symbol(0x0002, "$"),
			new Symbol(0x0003, "NEW_LINE"),
			new Symbol(0x0004, "WHITE_SPACE"),
			new Symbol(0x0005, "INTEGER"),
			new Symbol(0x0006, "REAL"),
			new Symbol(0x0007, "STRINGDQ"),
			new Symbol(0x0008, "STRINGSQ"),
			new Symbol(0x0009, "COMMENT_LINE"),
			new Symbol(0x000A, "IDENTIFIER"),
			new Symbol(0x000B, "SEPARATOR"),
			new Symbol(0x004E, "null"),
			new Symbol(0x004F, "TRUE"),
			new Symbol(0x0050, "FALSE"),
			new Symbol(0x0051, "DATE"),
			new Symbol(0x0052, ";"),
			new Symbol(0x0054, "-"),
			new Symbol(0x0055, "+"),
			new Symbol(0x0056, "*"),
			new Symbol(0x0057, "/"),
			new Symbol(0x0058, "||"),
			new Symbol(0x0059, "="),
			new Symbol(0x005A, "<>"),
			new Symbol(0x005B, ">"),
			new Symbol(0x005C, ">="),
			new Symbol(0x005D, "<"),
			new Symbol(0x005E, "<="),
			new Symbol(0x005F, ","),
			new Symbol(0x0061, "("),
			new Symbol(0x0062, ")"),
			new Symbol(0x0063, "TRIM"),
			new Symbol(0x0064, "LTRIM"),
			new Symbol(0x0065, "RTRIM"),
			new Symbol(0x0066, "LEADING"),
			new Symbol(0x0067, "TRAILING"),
			new Symbol(0x0068, "BOTH"),
			new Symbol(0x0069, "NOT"),
			new Symbol(0x006A, "AND"),
			new Symbol(0x006B, "OR"),
			new Symbol(0x006C, "DISTINCT"),
			new Symbol(0x006D, "ALL"),
			new Symbol(0x006E, "COUNT"),
			new Symbol(0x006F, "MAX"),
			new Symbol(0x0070, "MIN"),
			new Symbol(0x0071, "AVG"),
			new Symbol(0x0072, "COUNT(*)"),
			new Symbol(0x0074, "AS"),
			new Symbol(0x0075, "."),
			new Symbol(0x0076, "WHERE"),
			new Symbol(0x0077, "FROM"),
			new Symbol(0x0079, "SELECT") };
		/// <summary>
		/// Initializes a new instance of the lexer
		/// </summary>
		/// <param name="input">The lexer's input</param>
		public SqlLexer(string input) : base(commonAutomaton, terminals, 0x000B, input) {}
		/// <summary>
		/// Initializes a new instance of the lexer
		/// </summary>
		/// <param name="input">The lexer's input</param>
		public SqlLexer(TextReader input) : base(commonAutomaton, terminals, 0x000B, input) {}
	}
}

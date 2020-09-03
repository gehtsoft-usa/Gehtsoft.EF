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
			new Symbol(0x007B, "NULL"),
			new Symbol(0x007C, "TRUE"),
			new Symbol(0x007D, "FALSE"),
			new Symbol(0x007E, "DATE"),
			new Symbol(0x007F, "DATETIME"),
			new Symbol(0x0080, ";"),
			new Symbol(0x0082, "-"),
			new Symbol(0x0083, "+"),
			new Symbol(0x0084, "*"),
			new Symbol(0x0085, "/"),
			new Symbol(0x0086, "||"),
			new Symbol(0x0087, "="),
			new Symbol(0x0088, "<>"),
			new Symbol(0x0089, ">"),
			new Symbol(0x008A, ">="),
			new Symbol(0x008B, "<"),
			new Symbol(0x008C, "<="),
			new Symbol(0x008D, "LIKE"),
			new Symbol(0x008E, "NOT"),
			new Symbol(0x008F, "IN"),
			new Symbol(0x0090, "IS"),
			new Symbol(0x0091, ","),
			new Symbol(0x0093, "("),
			new Symbol(0x0094, ")"),
			new Symbol(0x0095, "ABS"),
			new Symbol(0x0096, "TOSTRING"),
			new Symbol(0x0097, "TOINTEGER"),
			new Symbol(0x0098, "TODOUBLE"),
			new Symbol(0x0099, "TODATE"),
			new Symbol(0x009A, "TOTIMESTAMP"),
			new Symbol(0x009B, "TRIM"),
			new Symbol(0x009C, "LTRIM"),
			new Symbol(0x009D, "RTRIM"),
			new Symbol(0x009E, "UPPER"),
			new Symbol(0x009F, "LOWER"),
			new Symbol(0x00A0, "CONTAINS"),
			new Symbol(0x00A1, "ENDSWITH"),
			new Symbol(0x00A2, "STARTSWITH"),
			new Symbol(0x00A3, "LEADING"),
			new Symbol(0x00A4, "TRAILING"),
			new Symbol(0x00A5, "BOTH"),
			new Symbol(0x00A7, "AND"),
			new Symbol(0x00A8, "OR"),
			new Symbol(0x00A9, "DISTINCT"),
			new Symbol(0x00AA, "ALL"),
			new Symbol(0x00AB, "COUNT"),
			new Symbol(0x00AC, "MAX"),
			new Symbol(0x00AD, "MIN"),
			new Symbol(0x00AE, "AVG"),
			new Symbol(0x00AF, "SUM"),
			new Symbol(0x00B0, "COUNT(*)"),
			new Symbol(0x00B2, "AS"),
			new Symbol(0x00B3, "."),
			new Symbol(0x00B4, "WHERE"),
			new Symbol(0x00B5, "FROM"),
			new Symbol(0x00B7, "AUTO"),
			new Symbol(0x00B8, "JOIN"),
			new Symbol(0x00B9, "INNER"),
			new Symbol(0x00BA, "OUTER"),
			new Symbol(0x00BB, "LEFT"),
			new Symbol(0x00BC, "RIGHT"),
			new Symbol(0x00BD, "FULL"),
			new Symbol(0x00BE, "ON"),
			new Symbol(0x00BF, "SELECT"),
			new Symbol(0x00C0, "ORDER BY"),
			new Symbol(0x00C2, "ASC"),
			new Symbol(0x00C3, "DESC"),
			new Symbol(0x00C4, "GROUP BY"),
			new Symbol(0x00C6, "LIMIT"),
			new Symbol(0x00C7, "OFFSET"),
			new Symbol(0x00C8, "INSERT"),
			new Symbol(0x00C9, "INTO"),
			new Symbol(0x00CC, "VALUES") };
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

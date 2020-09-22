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
			/// The unique identifier for terminal INT
			/// </summary>
			public const int TerminalInt = 0x0005;
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
			/// <summary>
			/// The unique identifier for terminal GLOBAL_PARAMETER_NAME
			/// </summary>
			public const int TerminalGlobalParameterName = 0x000C;
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
			new Symbol(0x0005, "INT"),
			new Symbol(0x0006, "REAL"),
			new Symbol(0x0007, "STRINGDQ"),
			new Symbol(0x0008, "STRINGSQ"),
			new Symbol(0x0009, "COMMENT_LINE"),
			new Symbol(0x000A, "IDENTIFIER"),
			new Symbol(0x000B, "SEPARATOR"),
			new Symbol(0x000C, "GLOBAL_PARAMETER_NAME"),
			new Symbol(0x009C, "NULL"),
			new Symbol(0x009D, "TRUE"),
			new Symbol(0x009E, "FALSE"),
			new Symbol(0x009F, "DATE"),
			new Symbol(0x00A0, "DATETIME"),
			new Symbol(0x00A1, ";"),
			new Symbol(0x00A3, "-"),
			new Symbol(0x00A4, "+"),
			new Symbol(0x00A5, "*"),
			new Symbol(0x00A6, "/"),
			new Symbol(0x00A7, "||"),
			new Symbol(0x00A8, "="),
			new Symbol(0x00A9, "<>"),
			new Symbol(0x00AA, ">"),
			new Symbol(0x00AB, ">="),
			new Symbol(0x00AC, "<"),
			new Symbol(0x00AD, "<="),
			new Symbol(0x00AE, "LIKE"),
			new Symbol(0x00AF, "NOT"),
			new Symbol(0x00B0, "IN"),
			new Symbol(0x00B1, "IS"),
			new Symbol(0x00B2, ","),
			new Symbol(0x00B4, "("),
			new Symbol(0x00B5, ")"),
			new Symbol(0x00B6, "ABS"),
			new Symbol(0x00B7, "TOSTRING"),
			new Symbol(0x00B8, "TOINT"),
			new Symbol(0x00B9, "TODOUBLE"),
			new Symbol(0x00BA, "TODATE"),
			new Symbol(0x00BB, "TOTIMESTAMP"),
			new Symbol(0x00BC, "TRIM"),
			new Symbol(0x00BD, "LTRIM"),
			new Symbol(0x00BE, "RTRIM"),
			new Symbol(0x00BF, "UPPER"),
			new Symbol(0x00C0, "LOWER"),
			new Symbol(0x00C1, "CONTAINS"),
			new Symbol(0x00C2, "ENDSWITH"),
			new Symbol(0x00C3, "STARTSWITH"),
			new Symbol(0x00C4, "LEADING"),
			new Symbol(0x00C5, "TRAILING"),
			new Symbol(0x00C6, "BOTH"),
			new Symbol(0x00C7, "LAST_RESULT"),
			new Symbol(0x00C8, "ROWS_COUNT"),
			new Symbol(0x00C9, "GET_ROW"),
			new Symbol(0x00CA, "GET_FIELD"),
			new Symbol(0x00CB, "NEW_ROWSET"),
			new Symbol(0x00CC, "NEW_ROW"),
			new Symbol(0x00CD, "STRING"),
			new Symbol(0x00CE, "INTEGER"),
			new Symbol(0x00CF, "DOUBLE"),
			new Symbol(0x00D0, "BOOLEAN"),
			new Symbol(0x00D1, "ROW"),
			new Symbol(0x00D2, "ROWSET"),
			new Symbol(0x00D3, "AS"),
			new Symbol(0x00D5, "AND"),
			new Symbol(0x00D6, "OR"),
			new Symbol(0x00D7, "DISTINCT"),
			new Symbol(0x00D8, "ALL"),
			new Symbol(0x00D9, "COUNT"),
			new Symbol(0x00DA, "MAX"),
			new Symbol(0x00DB, "MIN"),
			new Symbol(0x00DC, "AVG"),
			new Symbol(0x00DD, "SUM"),
			new Symbol(0x00DE, "COUNT(*)"),
			new Symbol(0x00E0, "."),
			new Symbol(0x00E1, "WHERE"),
			new Symbol(0x00E2, "FROM"),
			new Symbol(0x00E4, "AUTO"),
			new Symbol(0x00E5, "JOIN"),
			new Symbol(0x00E6, "INNER"),
			new Symbol(0x00E7, "OUTER"),
			new Symbol(0x00E8, "LEFT"),
			new Symbol(0x00E9, "RIGHT"),
			new Symbol(0x00EA, "FULL"),
			new Symbol(0x00EB, "ON"),
			new Symbol(0x00EC, "SELECT"),
			new Symbol(0x00ED, "ORDER BY"),
			new Symbol(0x00EF, "ASC"),
			new Symbol(0x00F0, "DESC"),
			new Symbol(0x00F1, "GROUP BY"),
			new Symbol(0x00F3, "LIMIT"),
			new Symbol(0x00F4, "OFFSET"),
			new Symbol(0x00F5, "INSERT"),
			new Symbol(0x00F6, "INTO"),
			new Symbol(0x00F9, "VALUES"),
			new Symbol(0x00FA, "UPDATE"),
			new Symbol(0x00FB, "SET"),
			new Symbol(0x00FD, "DELETE"),
			new Symbol(0x00FF, "DECLARE"),
			new Symbol(0x0101, "EXIT"),
			new Symbol(0x0102, "WITH"),
			new Symbol(0x0104, "IF"),
			new Symbol(0x0105, "THEN"),
			new Symbol(0x0106, "ELSIF"),
			new Symbol(0x0108, "ELSE"),
			new Symbol(0x0109, "END"),
			new Symbol(0x010A, "WHILE"),
			new Symbol(0x010B, "LOOP"),
			new Symbol(0x010C, "BREAK"),
			new Symbol(0x010D, "CONTINUE"),
			new Symbol(0x010E, "FOR"),
			new Symbol(0x010F, "NEXT"),
			new Symbol(0x0110, "SWITCH"),
			new Symbol(0x0111, "CASE"),
			new Symbol(0x0112, ":"),
			new Symbol(0x0114, "OTHERWISE"),
			new Symbol(0x0115, "ADD"),
			new Symbol(0x0116, "FIELD"),
			new Symbol(0x0117, "TO") };
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

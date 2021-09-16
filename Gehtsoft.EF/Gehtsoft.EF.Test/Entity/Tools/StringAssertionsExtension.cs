using System;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public static class StringAssertionsExtension
    {
        public static AndConstraint<StringAssertions> MatchPattern(this StringAssertions s, string pattern, string because = null, params object[] args)
            => MatchPattern(s, (QueryWithWhereBuilder)null, pattern, because, args);

        public static AndConstraint<StringAssertions> MatchPattern(this StringAssertions s, SelectEntitiesQueryBase query, string pattern, string because = null, params object[] args)
            => MatchPattern(s, query.SelectBuilder, pattern, because, args);

        public static AndConstraint<StringAssertions> MatchPattern(this StringAssertions s, AQueryBuilder query, string pattern, string because = null, params object[] args)
            => MatchPattern(s, query as QueryWithWhereBuilder, pattern, because, args);

        public static AndConstraint<StringAssertions> MatchPattern(this StringAssertions s, QueryWithWhereBuilder query, string pattern, string because = null, params object[] args)
        {
            var re = new Regex(ProcessRegex(pattern, query));
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => s.Subject)
                .ForCondition(m => re.IsMatch(m))
                .FailWith("Expected string {0} match pattern {1} but it does not", s.Subject, pattern);
            return new AndConstraint<StringAssertions>(s);
        }

        private static string ProcessRegex(string pattern, QueryWithWhereBuilder query)
        {
            StringBuilder r = new StringBuilder();
            r.Append('^');
            for (int i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];
                switch (c)
                {
                    case '@':
                        i++;
                        c = pattern[i];
                        switch (c)
                        {
                            case '@':
                                r.Append('@');
                                break;
                            case 'a':
                                r.Append(@"entity(\d+)");
                                break;
                            case 'p':
                                r.Append(@"@leq(\d+)");
                                break;
                            case 'w':
                                r.Append(@"(\w+)");
                                break;
                            case '%':
                                r.Append(".+");
                                break;
                            case '?':
                                r.Append(".");
                                break;
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                                if (query == null)
                                    throw new ArgumentException("Specify query in order to use references to the alias (e.g. @1)", nameof(pattern));
                                r.Append(query.Entities[c - '1'].Alias);
                                break;
                            default:
                                r.Append(c);
                                break;
                        }
                        break;
                    case ' ':
                        r.Append(@"\s*");
                        break;
                    case '˽':
                        r.Append(@"\s+");
                        break;
                    case '.':
                    case '(':
                    case ')':
                    case '>':
                    case '<':
                    case '/':
                    case '\\':
                    case '+':
                    case '*':
                    case '^':
                    case '|':
                    case '&':
                    case '$':
                        r.Append('\\').Append(c);
                        break;
                    default:
                        r.Append(c);
                        break;
                }
            }
            r.Append('$');
            return r.ToString();
        }
    }
}

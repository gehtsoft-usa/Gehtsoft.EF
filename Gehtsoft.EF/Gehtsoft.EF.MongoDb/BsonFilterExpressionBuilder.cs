using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    internal class BsonFilterExpressionBuilder
    {
        internal abstract class Element
        {
            public abstract bool IsEmpty { get; }

            public abstract bool IsOp { get; }

            public abstract SingleOp AsOp { get; }

            public abstract bool IsGroup { get; }

            public abstract Group AsGroup { get; }

            public abstract BsonDocument ToBsonDocument();
        }

        internal class SingleOp : Element
        {
            public override bool IsGroup => false;
            public override bool IsOp => true;
            public override bool IsEmpty => false;

            public BsonDocument Op { get; }

            [ExcludeFromCodeCoverage]
            public override SingleOp AsOp => this;

            [ExcludeFromCodeCoverage]
            public override Group AsGroup => null;

            public SingleOp(BsonDocument op)
            {
                Op = op;
            }

            public override BsonDocument ToBsonDocument()
            {
                return Op;
            }

            [ExcludeFromCodeCoverage]
            public override string ToString()
            {
                BsonElement el = Op.GetElement(0);
                BsonElement el1 = el.Value.AsBsonDocument.GetElement(0);
                return $"{el.Name} {el1.Name} {el1.Value}";
            }
        }

        internal class Group : Element
        {
            public override bool IsGroup => true;
            public override bool IsOp => false;
            public string LogOp { get; set; }
            public List<Element> Elements { get; } = new List<Element>();

            public override bool IsEmpty => Elements.Count == 0;

            [ExcludeFromCodeCoverage]
            public override SingleOp AsOp => null;

            public override Group AsGroup => this;

            public override BsonDocument ToBsonDocument()
            {
                if (Elements.Count == 0)
                    return new BsonDocument();
                else if (Elements.Count == 1)
                    return Elements[0].ToBsonDocument();
                else
                {
                    BsonArray array = new BsonArray();
                    foreach (Element element in Elements)
                        array.Add(element.ToBsonDocument());
                    return new BsonDocument(LogOp ?? "$and", array);
                }
            }

            [ExcludeFromCodeCoverage]
            public override string ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("(");

                foreach (Element element in Elements)
                {
                    if (builder.Length > 1)
                        builder.Append(' ').Append(LogOp ?? "$and").Append(' ');
                    builder.Append(element.ToString());
                }
                builder.Append(")");
                return builder.ToString();
            }
        }

        private Stack<Element> mElementStack = new Stack<Element>();

        private void Add(string logop, Element el)
        {
            Element top = mElementStack.Peek();
            if (top.IsOp)
            {
                //convert element into group
                Group group = new Group() { LogOp = logop };
                group.Elements.Add(top);
                group.Elements.Add(el);
                mElementStack.Pop();
                mElementStack.Push(group);
            }
            else if (top.IsGroup)
            {
                Group group = top.AsGroup;

                int groupSize = group.Elements.Count;
                if (groupSize == 0)
                {
                    group.LogOp = logop;
                }
                else
                {
                    if (logop == null)
                        logop = "$and";
                    if (group.LogOp == null)
                        group.LogOp = logop;
                    if (group.LogOp != logop)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.LogOpNotSame);
                }
                group.Elements.Add(el);
            }
            if (el.IsGroup)
                mElementStack.Push(el);
        }

        internal void Add(string logop, BsonDocument op)
        {
            Add(logop, new SingleOp(op));
        }

        public void BeginGroup(LogOp logop)
        {
            Add(ToMongoLogOp(logop), new Group());
        }

        public void EndGroup()
        {
            if (!mElementStack.Peek().IsGroup || mElementStack.Peek().AsGroup.Elements.Count == 0)
                throw new EfMongoDbException(EfMongoDbExceptionCode.FilterGroupIsEmpty);
            mElementStack.Pop();
        }

        public FilterDefinition<BsonDocument> ToBsonDocument()
        {
            if (mElementStack.Count != 1)
                throw new EfMongoDbException(EfMongoDbExceptionCode.FilterIsIncomplete);
            if (mElementStack.Peek().IsEmpty)
                return FilterDefinition<BsonDocument>.Empty;

            return mElementStack.Peek().ToBsonDocument();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            if (mElementStack.Count != 1)
                return "{Filter is not complete}";
            return mElementStack.Peek().ToString();
        }

        public BsonFilterExpressionBuilder()
        {
            mElementStack.Push(new Group() { LogOp = null });
        }

        [ExcludeFromCodeCoverage]
        [Obsolete("This legacy method should not be used")]
        public void Reset()
        {
            mElementStack = new Stack<Element>();
            mElementStack.Push(new Group() { LogOp = null });
        }

        public bool IsEmpty => mElementStack.Count == 1 && mElementStack.Peek().IsEmpty;

        private static BsonDocument ToMongoCmpOp(CmpOp cmpOp, object value)
        {
            switch (cmpOp)
            {
                case CmpOp.Eq:
                    return new BsonDocument("$eq", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Neq:
                    return new BsonDocument("$ne", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Gt:
                    return new BsonDocument("$gt", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Ge:
                    return new BsonDocument("$gte", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Ls:
                    return new BsonDocument("$lt", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Le:
                    return new BsonDocument("$lte", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.Like:
                    {
                        string svalue = (string)value;
                        if (!svalue.StartsWith("/"))
                        {
                            StringBuilder pattern = new StringBuilder();
                            for (int i = 0; i < svalue.Length; i++)
                            {
                                char c = svalue[i];
                                if (c == '%')
                                    pattern.Append(".*");
                                else if (c == '.')
                                    pattern.Append(".");
                                else if (c == '[')
                                {
                                    for (; i < svalue.Length; i++)
                                    {
                                        c = svalue[i];
                                        pattern.Append(c);
                                        if (c == ']')
                                            break;
                                    }
                                }
                                else
                                    pattern.Append(c);
                            }
                            svalue = pattern.ToString();
                        }
                        return new BsonDocument("$regex", EntityToBsonController.SerializeValue(svalue, null));
                    }
                case CmpOp.IsNull:
                    return new BsonDocument("$eq", EntityToBsonController.SerializeValue(null, null));

                case CmpOp.NotNull:
                    return new BsonDocument("$ne", EntityToBsonController.SerializeValue(null, null));

                case CmpOp.In:
                    return new BsonDocument("$in", EntityToBsonController.SerializeValue(value, null));

                case CmpOp.NotIn:
                    return new BsonDocument("$nin", EntityToBsonController.SerializeValue(value, null));

                default:
                    throw new EfMongoDbException(EfMongoDbExceptionCode.CmpOpNotSupported);
            }
        }

        private static string ToMongoLogOp(LogOp logOp)
        {
            if (logOp == LogOp.And)
                return "$and";
            if (logOp == LogOp.Or)
                return "$or";
            throw new EfMongoDbException(EfMongoDbExceptionCode.LogOpNotSupported);
        }

        private static BsonDocument OpToBson(string path, CmpOp op, object value) => new BsonDocument(path, ToMongoCmpOp(op, value));

        public void Add(string path, CmpOp cmpOp, object value) => Add(null, path, cmpOp, value);

        public void Add(LogOp? logOp, string path, CmpOp cmpOp, object value)
        {
            BsonDocument op = OpToBson(path, cmpOp, value);
            Add(logOp == null ? null : ToMongoLogOp(logOp.Value), op);
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ContractConfigurator.ExpressionParser
{
    /// <summary>
    /// Special expression parser subclass for Lists.  Automatically registered for every type registered.
    /// </summary>
    public class ListExpressionParser<T> : ClassExpressionParser<List<T>>
    {
        static Random r = new Random();

        static ListExpressionParser()
        {
            RegisterMethods();
        }

        public void RegisterExpressionParsers()
        {
            RegisterParserType(typeof(CelestialBody), typeof(CelestialBodyParser));
        }

        internal static void RegisterMethods()
        {
            RegisterMethod(new Method<List<T>, T>("Random", l => l.Skip(r.Next(l.Count)).First()));
        }

        public ListExpressionParser()
        {
        }

        internal override TResult ParseMethod<TResult>(Token token, List<T> obj, bool isFunction = false)
        {
            if (token.sval == "Where")
            {
                return ParseWhereMethod<TResult>(obj);
            }
            else
            {
                return base.ParseMethod<TResult>(token, obj, isFunction);
            }
        }

        internal TResult ParseWhereMethod<TResult>(List<T> obj)
        {
            verbose &= LogEntryDebug<TResult>("ParseWhereMethod", obj != null ? obj.ToString() : "null");
            try
            {
                // Start with method call
                ParseToken("(");

                // Get the identifier for the object
                Match m = Regex.Match(expression, @"([A-Za-z][A-Za-z0-9_]*)[\s]*=>[\s]*(.*)");
                string identifier = m.Groups[1].Value;
                expression = (string.IsNullOrEmpty(identifier) ? expression : m.Groups[2].Value);

                List<T> filteredList = new List<T>();

                // Save the expression, then execute for each value
                string savedExpression = expression;
                try
                {
                    foreach (T value in obj)
                    {
                        expression = savedExpression;
                        tempVariables[identifier] = value;
                        ExpressionParser<T> parser = GetParser<T>(this);
                        bool keep = parser.ParseStatement<bool>();
                        if (keep)
                        {
                            filteredList.Add(value);
                        }
                        expression = parser.expression;
                    }
                }
                finally
                {
                    if (tempVariables.ContainsKey(identifier))
                    {
                        tempVariables.Remove(identifier);
                    }
                }

                // Finish the method call
                ParseToken(")");

                // Check for a method call before we return
                Token methodToken = ParseMethodToken();
                ExpressionParser<TResult> retValParser = GetParser<TResult>(this);
                TResult result;
                if (methodToken != null)
                {
                    result = ParseMethod<TResult>(methodToken, filteredList);
                }
                else
                {
                    // No method, attempt to convert - most likely fails
                    result = retValParser.ConvertType(filteredList);
                }

                verbose &= LogExitDebug<TResult>("ParseWhereMethod", result);
                return result;
            }
            catch
            {
                verbose &= LogException<TResult>("ParseWhereMethod");
                throw;
            }
        }
    }
}

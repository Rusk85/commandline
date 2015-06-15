﻿// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine.Infrastructure;

namespace CommandLine.Core
{
    internal static class TokenPartitioner
    {
        public static
            TokenGroup Partition(
                IEnumerable<Token> tokens,
                Func<string, Maybe<Tuple<DescriptorType, Maybe<int>>>> typeLookup)
        {
            var tokenList = tokens.ToList();
            var switches = PartitionSwitches(tokenList, typeLookup).ToList();
            var tokensExceptSwitches = tokenList.Where(x => !switches.Contains(x)).ToList();
            var scalars = PartitionScalars(tokensExceptSwitches, typeLookup).ToList();
            var tokensExceptSwitchesAndScalars = (tokensExceptSwitches.Where(x => !scalars.Contains(x))).ToList();
            var sequences = PartitionSequences(tokensExceptSwitchesAndScalars, typeLookup).ToList();
            var tokensExceptSwitchesAndScalarsAndSeq = tokensExceptSwitchesAndScalars.Where(x => !sequences.Contains(x)).ToList();
            var values = tokensExceptSwitchesAndScalarsAndSeq.Where(v => v.IsValue()).ToList();
            var errors = tokensExceptSwitchesAndScalarsAndSeq.Where(x => !values.Contains(x));

            return TokenGroup.Create(
                    switches.Select(t => CreateValue(t.Text,"true"))
                        .Concat(scalars.Pairwise((f, s) => CreateValue(f.Text, s.Text)))
                        .Concat(SequenceTokensToKeyValuePairEnumerable(sequences)),
                values.Select(t => t.Text),
                errors);
        }

        private static IEnumerable<Token> PartitionSwitches(
            IEnumerable<Token> tokens,
            Func<string, Maybe<Tuple<DescriptorType, Maybe<int>>>> typeLookup)
        {
            return from t in tokens
                   where typeLookup(t.Text).Return(info => t.IsName() && info.Item1 == DescriptorType.Boolean, false)
                   select t;
        }

        private static IEnumerable<Token> PartitionScalars(
            IEnumerable<Token> tokens,
            Func<string, Maybe<Tuple<DescriptorType, Maybe<int>>>> typeLookup)
        {
            return from tseq in tokens.Pairwise(
                (f, s) =>
                        f.IsName() && s.IsValue()
                            ? typeLookup(f.Text).Return(info =>
                                    info.Item1 == DescriptorType.Scalar ? new[] { f, s } : new Token[] { }, new Token[] { })
                                    : new Token[] { })
                from t in tseq
                select t;
        }

        private static IEnumerable<Token> PartitionSequences(
            IEnumerable<Token> tokens,
            Func<string, Maybe<Tuple<DescriptorType, Maybe<int>>>> typeLookup)
        {
            return from tseq in tokens.Pairwise(
                (f, s) =>
                        f.IsName() && s.IsValue()
                            ? typeLookup(f.Text).Return(info =>
                                   info.Item1 == DescriptorType.Sequence
                                        ? new[] { f }.Concat(tokens.SkipWhile(t => t.Equals(f)).TakeWhile(v => v.IsValue()))
                                        : new Token[] { }, new Token[] { })
                            : new Token[] { })
                   from t in tseq
                   select t;
        }

        private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> SequenceTokensToKeyValuePairEnumerable(
            IEnumerable<Token> tokens)
        {
            return from t in tokens.Pairwise(
                (f, s) =>
                        f.IsName()
                            ? CreateValue(f.Text, tokens.SkipWhile(t => t.Equals(f)).TakeWhile(v => v.IsValue()).Select(x => x.Text).ToArray())
                            : CreateValue(string.Empty))
                   where t.Key.Length > 0 && t.Value.Any()
                   select t;
        }

        private static KeyValuePair<string, IEnumerable<string>> CreateValue(string value, params string[] values)
        {
            return new KeyValuePair<string, IEnumerable<string>>(value, values);
        }
    }
}
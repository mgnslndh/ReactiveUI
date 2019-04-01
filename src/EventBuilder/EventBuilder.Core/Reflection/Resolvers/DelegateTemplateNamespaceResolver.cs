// Copyright (c) 2019 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using EventBuilder.Core.Reflection.Generators;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EventBuilder.Core.Reflection.Resolvers
{
    internal class DelegateTemplateNamespaceResolver : INamespaceResolver
    {
        private const string DelegateName = "MulticastDelegate";
        private static readonly string[] CocoaDelegateNames =
        {
            "Delegate",
            "UITableViewSource"
        };

        private static readonly string[] BannedMethods =
        {
            "Dispose",
            "Finalize"
        };

        // NB: Apparently this used to break "build on device because of reasons". We don't know what these reasons are and this may not be needed anymore.
        private static readonly string[] _garbageTypeList = { "AVPlayerItemLegibleOutputPushDelegate" };

        public IEnumerable<NamespaceDeclarationSyntax> Create(ICompilation compilation)
        {
            IEnumerable<(ITypeDefinition typeDefinition, IEnumerable<IMethod> methods)> values = compilation.GetPublicNonGenericTypeDefinitions()
                .Where(
                    x => x.Kind != TypeKind.Interface
                    && (!IsMulticastDelegateDerived(x)
                    || !x.DirectBaseTypes.Any())
                    && !_garbageTypeList.Any(y => x.FullName.Contains(y))
                    && CocoaDelegateNames.Any(cocoaName => x.FullName.EndsWith(cocoaName, StringComparison.OrdinalIgnoreCase)))
                .Select(x => (x, GetPublicDelegateMethods(x)))
                .Where(x => x.Item2.Any());

            return DelegateGenerator.Generate(values);
        }

        private static bool IsMulticastDelegateDerived(ITypeDefinition typeDefinition)
        {
            return typeDefinition.DirectBaseTypes.Any(x => x.FullName.Contains(DelegateName));
        }

        private static IEnumerable<IMethod> GetPublicDelegateMethods(ITypeDefinition typeDefinition)
        {
            return typeDefinition.Methods
                .Where(x => x.IsVirtual && !x.IsConstructor && !x.IsAccessor && x.ReturnType.FullName == "System.Void" && x.Parameters.All(y => !y.IsRef) && !BannedMethods.Contains(x.Name))
                .GroupBy(x => x.Name)
                .Select(x => x.OrderByDescending(y => y.Parameters.Count).First());
        }
    }
}

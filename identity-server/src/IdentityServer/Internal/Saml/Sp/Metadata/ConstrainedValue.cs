// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class ClaimValue
    {
        public string Value { get; set; }
        public ICollection<XmlElement> StructuredValue { get; set; }
    }

    internal class ConstrainedValue
    {
        internal abstract class Constraint
        {
        }

        internal class CompareConstraint : Constraint
        {
            internal enum CompareOperator
            {
                Lt,
                Lte,
                Gt,
                Gte,
            }
            public CompareOperator CompareOp { get; private set; }
            public ClaimValue Value { get; set; } = new ClaimValue();

            public CompareConstraint(CompareOperator compareOp)
            {
                CompareOp = compareOp;
            }
        }

        internal class RangeConstraint : Constraint
        {
            public ClaimValue LowerBound { get; set; } = new ClaimValue();
            public ClaimValue UpperBound { get; set; } = new ClaimValue();
        }

        internal class ListConstraint : Constraint
        {
            public ICollection<ClaimValue> Values { get; private set; } =
                new Collection<ClaimValue>();
        }

        public bool AssertConstraint { get; set; }
        public ICollection<Constraint> Constraints { get; private set; } =
            new Collection<Constraint>();
    }
}

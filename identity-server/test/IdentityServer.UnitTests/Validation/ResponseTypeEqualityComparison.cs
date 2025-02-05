// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;
using Shouldly;
using Xunit;

namespace UnitTests.Validation;

/// <summary>
/// Tests for ResponseTypeEqualityComparer
/// </summary>
/// <remarks>
/// Some of these are pretty fundamental equality checks, but the purpose here is to ensure the
/// important property: that the order is insignificant when multiple values are
/// sent in a space-delimited string.  We want to ensure that property holds and at the same time
/// the basic equality function works as well.
/// </remarks>
public class ResponseTypeEqualityComparison
{
    /// <summary>
    /// These tests ensure that single-value strings compare with the
    /// same behavior as default string comparisons.
    /// </summary>
    public class SingleValueStringComparisons
    {
        [Fact]
        public void Both_null()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = null;
            string y = null;
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }

        [Fact]
        public void Left_null_other_not()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = null;
            string y = string.Empty;
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }

        [Fact]
        public void Right_null_other_not()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = string.Empty;
            string y = null;
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }

        [Fact]
        public void token_token()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "token";
            string y = "token";
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }

        [Fact]
        public void id_token_id_token()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "id_token";
            string y = "id_token";
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }

        [Fact]
        public void id_token_token()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "id_token";
            string y = "token";
            var result = comparer.Equals(x, y);
            var expected = (x == y);
            result.ShouldBe(expected);
        }
    }

    /// <summary>
    /// These tests ensure the property demanded by the 
    /// <see href="https://tools.ietf.org/html/rfc6749#section-3.1.1">OAuth2 spec</see>
    /// where, in a space-delimited list of values, the order is not important.
    /// </summary>
    public class MultipleValueStringComparisons
    {
        [Fact]
        public void id_token_token_both_ways()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "id_token token";
            string y = "token id_token";
            var result = comparer.Equals(x, y);
            result.ShouldBeTrue();
        }

        [Fact]
        public void code_id_token_both_ways()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token";
            string y = "id_token code";
            var result = comparer.Equals(x, y);
            result.ShouldBeTrue();
        }

        [Fact]
        public void code_token_both_ways()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code token";
            string y = "token code";
            var result = comparer.Equals(x, y);
            result.ShouldBeTrue();
        }

        [Fact]
        public void code_id_token_token_combo1()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token token";
            string y = "id_token code token";
            var result = comparer.Equals(x, y);
            result.ShouldBeTrue();
        }

        [Fact]
        public void code_id_token_token_combo2()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token token";
            string y = "token id_token code";
            var result = comparer.Equals(x, y);
            result.ShouldBeTrue();
        }

        [Fact]
        public void code_id_token_token_missing_code()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token token";
            string y = "id_token token";
            var result = comparer.Equals(x, y);
            result.ShouldBeFalse();
        }

        [Fact]
        public void code_id_token_token_missing_code_and_token()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token token";
            string y = "id_token";
            var result = comparer.Equals(x, y);
            result.ShouldBeFalse();
        }

        [Fact]
        public void Totally_different_words()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "blerg smoo";
            string y = "token code";
            var result = comparer.Equals(x, y);
            result.ShouldBeFalse();
        }

        [Fact]
        public void Same_length_different_count()
        {
            ResponseTypeEqualityComparer comparer = new ResponseTypeEqualityComparer();
            string x = "code id_token token";
            string y = "tokenizer bleegerfi";
            var result = comparer.Equals(x, y);
            result.ShouldBeFalse();
        }
    }
}
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;

using NgSharp;
using NgSharp.Pipes;
using NgSharp.Parsing;
using NgSharp.Rendering;

namespace NgSharp.Tests.Expressions;

public class ExpressionTests
{
    private static readonly IReadOnlyDictionary<string, IPipe> Pipes = new Dictionary<string, IPipe>
    {
        ["upper"] = new UpperPipe(),
        ["number"] = new NumberPipe()
    };

    [Fact]
    public void Evaluates_Integer_Literal()
        => Assert.Equal(42, Eval("42").GetInt());

    [Fact]
    public void Evaluates_String_Literal_As_String_Not_Number()
        => Assert.Equal("123", Eval("'123'").GetString());

    [Fact]
    public void Evaluates_Null_Literal()
        => Assert.Equal(JsonValueKind.Null, Eval("null").ValueKind);

    [Fact]
    public void Evaluates_Boolean_Literal()
        => Assert.True(Eval("true").GetBoolean().Value);

    [Fact]
    public void Evaluates_A_Nested_Path()
        => Assert.Equal("Alice", Eval("User.Name", new { User = new { Name = "Alice" } }).GetString());

    [Fact]
    public void Numeric_Equality_True()
        => Assert.True(Eval("Count == 3", new { Count = 3 }).GetBoolean().Value);

    [Fact]
    public void Numeric_Equality_False()
        => Assert.False(Eval("Count == 5", new { Count = 3 }).GetBoolean().Value);

    [Fact]
    public void Numeric_Inequality_True()
        => Assert.True(Eval("Count != 5", new { Count = 3 }).GetBoolean().Value);

    [Fact]
    public void Negative_Number_Literal_Compares_Correctly()
        => Assert.True(Eval("V == -3", new { V = -3 }).GetBoolean().Value);

    [Fact]
    public void Equality_With_Null_When_Value_Is_Null()
        => Assert.True(Eval("Photo == null", new { Photo = (string)null }).GetBoolean().Value);

    [Fact]
    public void Equality_With_Null_When_Value_Present()
        => Assert.False(Eval("Photo == null", new { Photo = "a.jpg" }).GetBoolean().Value);

    [Fact]
    public void Ternary_Selects_When_True()
        => Assert.Equal("yes", Eval("Ok == true ? 'yes' : 'no'", new { Ok = true }).GetString());

    [Fact]
    public void Ternary_Selects_When_False()
        => Assert.Equal("no", Eval("Ok == true ? 'yes' : 'no'", new { Ok = false }).GetString());

    [Fact]
    public void Ternary_With_Null_Condition_Selects_Correctly()
        => Assert.Equal("gray", Eval("Photo == null ? 'gray' : 'image'", new { Photo = (string)null }).GetString());

    [Fact]
    public void Pipe_Transforms_The_Value()
        => Assert.Equal("ALICE", Eval("Name | upper", new { Name = "alice" }, Pipes).GetString());

    [Fact]
    public void Chained_Pipes_Apply_Left_To_Right()
        => Assert.Equal("ALICE", Eval("Name | upper | upper", new { Name = "alice" }, Pipes).GetString());

    [Fact]
    public void Pipe_With_Argument_Formats_The_Value()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("12,345", Eval("Amount | number: 'N0'", new { Amount = 12345 }, Pipes).GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    #region Logical operators ( && , || , short-circuit, precedence, grouping )

    [Fact]
    public void And_Both_True_Is_True()
        => Assert.True(Eval("A == 1 && B == 2", new { A = 1, B = 2 }).GetBoolean());

    [Fact]
    public void And_Second_Operand_False_Is_False()
        => Assert.False(Eval("A == 1 && B == 2", new { A = 1, B = 9 }).GetBoolean());

    [Fact]
    public void And_First_Operand_False_Is_False()
        => Assert.False(Eval("A == 1 && B == 2", new { A = 9, B = 2 }).GetBoolean());

    [Fact]
    public void Or_Second_Operand_True_Is_True()
        => Assert.True(Eval("A == 1 || B == 2", new { A = 9, B = 2 }).GetBoolean());

    [Fact]
    public void Or_Both_Operands_False_Is_False()
        => Assert.False(Eval("A == 1 || B == 2", new { A = 9, B = 8 }).GetBoolean());

    [Fact]
    public void And_Binds_Tighter_Than_Or()
        => Assert.True(Eval("A == 1 || B == 9 && C == 9", new { A = 1, B = 2, C = 3 }).GetBoolean());

    [Fact]
    public void Parentheses_Override_Precedence()
        => Assert.False(Eval("(A == 1 || B == 9) && C == 9", new { A = 1, B = 2, C = 3 }).GetBoolean());

    [Fact]
    public void And_On_Boolean_Fields_Without_Explicit_Comparison()
        => Assert.True(Eval("IsActive && HasAccess", new { IsActive = true, HasAccess = true }).GetBoolean());

    [Fact]
    public void And_Second_Boolean_Field_False_Is_False()
        => Assert.False(Eval("IsActive && HasAccess", new { IsActive = true, HasAccess = false }).GetBoolean());

    [Fact]
    public void Missing_Operand_Is_Not_Truthy_No_Coercion()
        => Assert.False(Eval("Missing && A == 1", new { A = 1 }).GetBoolean());

    [Fact]
    public void Explicit_Null_Check_Combined_With_And()
        => Assert.True(Eval("Name != null && A == 1", new { Name = "x", A = 1 }).GetBoolean());

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void And_TruthTable(bool l, bool r, bool expected)
        => Assert.Equal(expected, Eval("L && R", new { L = l, R = r }).GetBoolean());

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Or_TruthTable(bool l, bool r, bool expected)
        => Assert.Equal(expected, Eval("L || R", new { L = l, R = r }).GetBoolean());

    [Fact]
    public void And_ShortCircuits_Right_Skipped_When_Left_False()
        => Assert.False(Eval("L && (X | nope)", new { L = false, X = "y" }).GetBoolean());

    [Fact]
    public void Or_ShortCircuits_Right_Skipped_When_Left_True()
        => Assert.True(Eval("L || (X | nope)", new { L = true, X = "y" }).GetBoolean());

    [Fact]
    public void And_Evaluates_Right_When_Left_True_So_Bad_Pipe_Throws()
        => Assert.ThrowsAny<System.Exception>(() => Eval("L && (X | nope)", new { L = true, X = "y" }));

    [Fact]
    public void Or_Evaluates_Right_When_Left_False_So_Bad_Pipe_Throws()
        => Assert.ThrowsAny<System.Exception>(() => Eval("L || (X | nope)", new { L = false, X = "y" }));

    [Fact]
    public void Chained_And_ShortCircuits_Before_Reaching_Bad_Pipe()
        => Assert.False(Eval("L && M && (X | nope)", new { L = false, M = true, X = "y" }).GetBoolean());

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    public void And_Chained_Three(bool a, bool b, bool c, bool expected)
        => Assert.Equal(expected, Eval("A && B && C", new { A = a, B = b, C = c }).GetBoolean());

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    public void Or_Chained_Three(bool a, bool b, bool c, bool expected)
        => Assert.Equal(expected, Eval("A || B || C", new { A = a, B = b, C = c }).GetBoolean());

    [Fact]
    public void And_Chained_Five_All_True()
        => Assert.True(Eval("A && B && C && D && E", new { A = true, B = true, C = true, D = true, E = true }).GetBoolean());

    [Fact]
    public void And_Chained_Five_One_False()
        => Assert.False(Eval("A && B && C && D && E", new { A = true, B = true, C = false, D = true, E = true }).GetBoolean());

    [Theory]
    [InlineData(false, true, true, true)]    // F || (T && T) = T
    [InlineData(false, true, false, false)]  // F || (T && F) = F
    [InlineData(true, false, false, true)]   // T || (F && F) = T   — distinguishes from (A||B)&&C
    public void Or_Is_Lower_Precedence_Than_And(bool a, bool b, bool c, bool expected)
        => Assert.Equal(expected, Eval("A || B && C", new { A = a, B = b, C = c }).GetBoolean());

    [Theory]
    [InlineData(false, false, true, true)]   // (F && F) || T = T   — distinguishes from A&&(B||C)
    [InlineData(true, true, false, true)]    // (T && T) || F = T
    [InlineData(true, false, false, false)]  // (T && F) || F = F
    public void And_Is_Higher_Precedence_Than_Or(bool a, bool b, bool c, bool expected)
        => Assert.Equal(expected, Eval("A && B || C", new { A = a, B = b, C = c }).GetBoolean());

    [Fact]
    public void Parens_Double_Nested()
        => Assert.True(Eval("((A == 1))", new { A = 1 }).GetBoolean());

    [Theory]
    [InlineData(true, false, false, false)]  // (T || F) && F = F
    [InlineData(true, false, true, true)]    // (T || F) && T = T
    [InlineData(false, false, true, false)]  // (F || F) && T = F
    public void Parens_Force_Or_Before_And(bool a, bool b, bool c, bool expected)
        => Assert.Equal(expected, Eval("(A || B) && C", new { A = a, B = b, C = c }).GetBoolean());

    [Theory]
    [InlineData(true, true, false, false, true)]   // (T&&T) || (F&&F) = T
    [InlineData(false, true, true, true, true)]    // (F&&T) || (T&&T) = T
    [InlineData(true, false, true, false, false)]  // (T&&F) || (T&&F) = F
    public void Parens_Grouped_On_Both_Sides(bool a, bool b, bool c, bool d, bool expected)
        => Assert.Equal(expected, Eval("(A && B) || (C && D)", new { A = a, B = b, C = c, D = d }).GetBoolean());

    [Fact]
    public void Number_Operand_Is_Falsy_Even_When_Nonzero()
        => Assert.False(Eval("Count && Flag", new { Count = 5, Flag = true }).GetBoolean());

    [Fact]
    public void Zero_Operand_Is_Also_Falsy_Not_Special()
        => Assert.False(Eval("Count && Flag", new { Count = 0, Flag = true }).GetBoolean());

    [Fact]
    public void String_Operand_Is_Falsy()
        => Assert.False(Eval("Name && Flag", new { Name = "hello", Flag = true }).GetBoolean());

    [Fact]
    public void Null_Literal_Operand_Is_Falsy()
        => Assert.False(Eval("null && Flag", new { Flag = true }).GetBoolean());

    [Fact]
    public void Number_Compared_Then_Anded_Is_True()
        => Assert.True(Eval("Count > 0 && Flag", new { Count = 5, Flag = true }).GetBoolean());

    [Fact]
    public void Mixed_Comparison_Operators_Anded()
        => Assert.True(Eval("A >= 1 && B <= 5 && C != 0", new { A = 3, B = 4, C = 1 }).GetBoolean());

    [Fact]
    public void Mixed_Comparison_Operators_Ored()
        => Assert.True(Eval("A > 5 || B < 2", new { A = 1, B = 1 }).GetBoolean());

    [Fact]
    public void No_Spaces_Around_Operators()
        => Assert.True(Eval("A==1&&B==2", new { A = 1, B = 2 }).GetBoolean());

    [Fact]
    public void Extra_Spaces_Around_Operators()
        => Assert.True(Eval("A  ==  1   &&   B  ==  2", new { A = 1, B = 2 }).GetBoolean());

    [Fact]
    public void And_As_Ternary_Condition_Selects_True_Branch()
        => Assert.Equal("yes", Eval("A == 1 && B == 2 ? 'yes' : 'no'", new { A = 1, B = 2 }).GetString());

    [Fact]
    public void And_As_Ternary_Condition_Selects_False_Branch()
        => Assert.Equal("no", Eval("A == 1 && B == 2 ? 'yes' : 'no'", new { A = 1, B = 9 }).GetString());

    [Fact]
    public void Or_As_Ternary_Condition()
        => Assert.Equal("yes", Eval("A == 1 || B == 9 ? 'yes' : 'no'", new { A = 1, B = 9 }).GetString());

    #endregion

    #region Unary '!' ( negation, strict truthiness, precedence )

    [Fact]
    public void Not_Negates_True_To_False()
        => Assert.False(Eval("!Flag", new { Flag = true }).GetBoolean());

    [Fact]
    public void Not_Negates_False_To_True()
        => Assert.True(Eval("!Flag", new { Flag = false }).GetBoolean());

    [Fact]
    public void Not_Of_Missing_Is_True_Strict_Truthiness()
        => Assert.True(Eval("!Missing", new { A = 1 }).GetBoolean());

    [Fact]
    public void Not_Of_NonBool_Value_Is_True_Strict_Truthiness()
        => Assert.True(Eval("!Name", new { Name = "hello" }).GetBoolean());

    [Fact]
    public void Double_Not_Preserves_Truthiness()
    {
        Assert.True(Eval("!!Flag", new { Flag = true }).GetBoolean());
        Assert.False(Eval("!!Flag", new { Flag = false }).GetBoolean());
    }

    [Fact]
    public void Not_Binds_Tighter_Than_And()
        => Assert.True(Eval("!A && B", new { A = false, B = true }).GetBoolean());

    [Fact]
    public void Not_Applies_To_Parenthesized_Comparison()
        => Assert.True(Eval("!(A == 1)", new { A = 2 }).GetBoolean());

    [Fact]
    public void Not_On_Comparison_Left_Operand()
        => Assert.True(Eval("!A == false", new { A = true }).GetBoolean());

    [Fact]
    public void Not_As_Ternary_Condition()
        => Assert.Equal("hidden", Eval("!Visible ? 'hidden' : 'shown'", new { Visible = false }).GetString());

    #endregion

    #region Safe-navigation '?.' ( null-safe member access — resolves like '.', never misparses as a ternary )

    [Fact]
    public void SafeNav_Resolves_Present_Member()
        => Assert.Equal("Alice", Eval("User?.Name", new { User = new { Name = "Alice" } }).GetString());

    [Fact]
    public void SafeNav_On_Null_Intermediate_Yields_Null_Not_Wrong_Branch()
        => Assert.Equal(JsonValueKind.Null, Eval("User?.Name", new { User = (string)null }).ValueKind);

    [Fact]
    public void SafeNav_On_Missing_Intermediate_Yields_Null()
        => Assert.Equal(JsonValueKind.Null, Eval("User?.Name", new { Other = 1 }).ValueKind);

    [Fact]
    public void SafeNav_Chained_Resolves_Deep_Member()
        => Assert.Equal("Paris", Eval("User?.Address?.City", new { User = new { Address = new { City = "Paris" } } }).GetString());

    [Fact]
    public void SafeNav_Chained_On_Null_Middle_Yields_Null()
        => Assert.Equal(JsonValueKind.Null, Eval("User?.Address?.City", new { User = new { Address = (string)null } }).ValueKind);

    [Fact]
    public void SafeNav_Does_Not_Swallow_A_Following_Ternary()
        => Assert.Equal("y", Eval("A?.B == 1 ? 'y' : 'n'", new { A = new { B = 1 } }).GetString());

    #endregion

    #region Arithmetic ( + - * / % , precedence, unary minus, '+' string concat )

    [Fact]
    public void Adds_Two_Numbers()
        => Assert.Equal(5, Eval("A + B", new { A = 2, B = 3 }).GetInt());

    [Fact]
    public void Subtracts_Two_Numbers()
        => Assert.Equal(1, Eval("A - B", new { A = 3, B = 2 }).GetInt());

    [Fact]
    public void Multiplies_Two_Numbers()
        => Assert.Equal(12, Eval("A * B", new { A = 3, B = 4 }).GetInt());

    [Fact]
    public void Divides_Two_Numbers()
        => Assert.Equal(2.5, Eval("A / B", new { A = 5, B = 2 }).GetDouble());

    [Fact]
    public void Modulo_Of_Two_Numbers()
        => Assert.Equal(1, Eval("A % B", new { A = 7, B = 3 }).GetInt());

    [Fact]
    public void Multiplication_Binds_Tighter_Than_Addition()
        => Assert.Equal(14, Eval("A + B * C", new { A = 2, B = 3, C = 4 }).GetInt());   // 2 + (3*4)

    [Fact]
    public void Parentheses_Override_Arithmetic_Precedence()
        => Assert.Equal(20, Eval("(A + B) * C", new { A = 2, B = 3, C = 4 }).GetInt()); // (2+3)*4

    [Fact]
    public void Subtraction_Is_Left_Associative()
        => Assert.Equal(0, Eval("A - B - C", new { A = 5, B = 3, C = 2 }).GetInt());    // (5-3)-2

    [Fact]
    public void Unary_Minus_Negates()
        => Assert.Equal(-3, Eval("-A", new { A = 3 }).GetInt());

    [Fact]
    public void Unary_Minus_As_A_Subtrahend()
        => Assert.Equal(5, Eval("A - -B", new { A = 3, B = 2 }).GetInt());              // 3 - (-2)

    [Fact]
    public void Negative_Literal_Still_Compares()
        => Assert.True(Eval("V == -3", new { V = -3 }).GetBoolean());

    [Fact]
    public void Plus_Concatenates_When_An_Operand_Is_A_String()
        => Assert.Equal("Alice Martin", Eval("First + ' ' + Last", new { First = "Alice", Last = "Martin" }).GetString());

    [Fact]
    public void Plus_Concatenates_A_String_And_A_Number()
        => Assert.Equal("Total: 5", Eval("'Total: ' + N", new { N = 5 }).GetString());

    [Fact]
    public void Arithmetic_Binds_Tighter_Than_Comparison()
        => Assert.True(Eval("A + B == C", new { A = 2, B = 3, C = 5 }).GetBoolean());   // (2+3)==5

    [Fact]
    public void Division_By_Zero_Is_Zero_Not_Infinity()
        => Assert.Equal(0, Eval("A / B", new { A = 5, B = 0 }).GetInt());

    [Fact]
    public void Missing_Operand_Counts_As_Zero_In_Arithmetic()
        => Assert.Equal(3, Eval("Missing + A", new { A = 3 }).GetInt());

    #endregion

    #region Array indexing ( Items[0].Name — the README lists it; was silently broken in expressions )

    [Fact]
    public void Indexes_The_First_Array_Element()
        => Assert.Equal("Alice", Eval("Users[0].Name", new { Users = new[] { new { Name = "Alice" }, new { Name = "Bob" } } }).GetString());

    [Fact]
    public void Indexes_A_Later_Array_Element()
        => Assert.Equal("Bob", Eval("Users[1].Name", new { Users = new[] { new { Name = "Alice" }, new { Name = "Bob" } } }).GetString());

    [Fact]
    public void Out_Of_Range_Index_Is_Null()
        => Assert.Equal(JsonValueKind.Null, Eval("Users[9].Name", new { Users = new[] { new { Name = "Alice" } } }).ValueKind);

    [Fact]
    public void Indexes_A_Scalar_Array_Element()
        => Assert.Equal("b", Eval("Tags[1]", new { Tags = new[] { "a", "b", "c" } }).GetString());

    [Fact]
    public void Index_Composes_With_Safe_Navigation()
        => Assert.Equal("Alice", Eval("Users[0]?.Name", new { Users = new[] { new { Name = "Alice" } } }).GetString());

    #endregion

    private static NgElement Ctx(object model)
    {
        var json = JsonSerializer.Serialize(model);
        using var doc = JsonDocument.Parse(json);

        return NgElement.FromJson(doc.RootElement.Clone());
    }

    private static NgElement Eval(string expression, object model = null)
        => ExpressionEvaluator.Evaluate(ExpressionParser.Parse(expression), model is null ? default : Ctx(model));

    private static NgElement Eval(string expression, object model, IReadOnlyDictionary<string, IPipe> pipes)
        => ExpressionEvaluator.Evaluate(ExpressionParser.Parse(expression), model is null ? default : Ctx(model), pipes);
}

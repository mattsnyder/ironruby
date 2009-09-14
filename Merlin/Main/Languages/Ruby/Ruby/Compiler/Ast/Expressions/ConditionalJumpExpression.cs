﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if !CLR2
using MSA = System.Linq.Expressions;
#else
using MSA = Microsoft.Scripting.Ast;
#endif

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronRuby.Compiler.Ast {
    using Ast = MSA.Expression;

    /// <summary>
    /// Represents {condition} {and/or/&&/||} {jump-statement}, 
    /// or {condition} ? {jump-statement} : {value}.
    /// </summary>
    public partial class ConditionalJumpExpression : Expression {
        private readonly bool _negateCondition;
        private readonly Expression/*!*/ _condition;
        private readonly Expression _value;
        private readonly JumpStatement/*!*/ _jumpStatement;

        public bool NegateCondition {
            get { return _negateCondition; }
        }

        public bool IsBooleanExpression {
            get { return _value == null; }
        }

        public Expression/*!*/ Condition {
            get { return _condition; }
        }

        public Expression Value {
            get { return _value; }
        }

        public JumpStatement/*!*/ JumpStatement {
            get { return _jumpStatement; }
        }

        public ConditionalJumpExpression(Expression/*!*/ condition, JumpStatement/*!*/ jumpStatement, bool negateCondition, Expression value, SourceSpan location)
            : base(location) {
            ContractUtils.RequiresNotNull(condition, "condition");
            ContractUtils.RequiresNotNull(jumpStatement, "jumpStatement");

            _condition = condition;
            _jumpStatement = jumpStatement;
            _negateCondition = negateCondition;
            _value = value;
        }

        internal override MSA.Expression/*!*/ TransformRead(AstGenerator/*!*/ gen) {
            MSA.Expression transformedCondition = AstFactory.Box(_condition.TransformRead(gen));
            MSA.Expression tmpVariable = gen.CurrentScope.DefineHiddenVariable("#tmp_cond", transformedCondition.Type);
            
            return AstFactory.Block(
                Ast.Assign(tmpVariable, transformedCondition),
                AstUtils.IfThen(
                    (_negateCondition ? AstFactory.IsFalse(tmpVariable) : AstFactory.IsTrue(tmpVariable)),
                    _jumpStatement.Transform(gen)
                ),
                (_value != null) ? _value.TransformRead(gen) : tmpVariable
            );
        }
    }
}

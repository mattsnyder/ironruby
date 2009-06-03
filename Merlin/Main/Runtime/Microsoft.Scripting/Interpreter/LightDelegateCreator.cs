﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

namespace Microsoft.Scripting.Interpreter {
    
    /// <summary>
    /// Manages creation of interpreted delegates. These delegates will get
    /// compiled if they are executed often enough.
    /// </summary>
    internal sealed class LightDelegateCreator {
        private readonly Interpreter _interpreter;
        private readonly LambdaExpression _lambda;
        private readonly IList<ParameterExpression> _closureVariables;

        // Adaptive compilation support:
        private Type _compiledDelegateType;
        private Delegate _compiled;
        private int _executionCount;
        private readonly object _compileLock = new object();

        private const int CompilationThreshold = 32;

        internal LightDelegateCreator(Interpreter interpreter, LambdaExpression lambda, IList<ParameterExpression> closureVariables) {
            _interpreter = interpreter;
            _lambda = lambda;
            _closureVariables = closureVariables;
        }

        internal IList<ParameterExpression> ClosureVariables {
            get { return _closureVariables; }
        }

        internal Interpreter Interpreter {
            get { return _interpreter; }
        }

        private bool HasClosure {
            get { return _closureVariables.Count > 0; }
        }

        internal bool HasCompiled {
            get { return _compiled != null; }
        }

        /// <summary>
        /// true if the compiled delegate has the same type as the lambda;
        /// false if the type was changed for interpretation.
        /// </summary>
        internal bool SameDelegateType {
            get { return _compiledDelegateType == _lambda.Type; }
        }

        internal Delegate CreateDelegate() {
            return CreateDelegate(null);
        }

        internal Delegate CreateDelegate(StrongBox<object>[] closure) {
            if (_compiled != null) {
                // If the delegate type we want is not a Func/Action, we can't
                // use the compiled code directly. So instead just fall through
                // and create an interpreted LightLambda, which will pick up
                // the compiled delegate on its first run.
                //
                // Ideally, we would just rebind the compiled delegate using
                // Delgate.CreateDelegate. Unfortunately, it doesn't work on
                // dynamic methods.
                if (SameDelegateType) {
                    return CreateCompiledDelegate(closure);
                }
            }

            if (_interpreter == null) {
                // We can't interpret, so force a compile
                Compile(null);
                Delegate compiled = CreateCompiledDelegate(closure);
                Debug.Assert(compiled.GetType() == _lambda.Type);
                return compiled;
            }

            // Otherwise, we'll create an interpreted LightLambda
            return new LightLambda(this, closure).MakeDelegate(_lambda.Type);
        }

        /// <summary>
        /// Used by LightLambda to get the compiled delegate.
        /// </summary>
        internal Delegate CreateCompiledDelegate(StrongBox<object>[] closure) {
            Debug.Assert(HasClosure == (closure != null));

            if (HasClosure) {
                // We need to apply the closure to get the actual delegate.
                var applyClosure = (Func<StrongBox<object>[], Delegate>)_compiled;
                return applyClosure(closure);
            }
            return _compiled;
        }

        /// <summary>
        /// Create a compiled delegate for the LightLambda, and saves it so
        /// future calls to Run will execute the compiled code instead of
        /// interpreting.
        /// </summary>
        internal void Compile(object state) {
            if (_compiled != null) {
                return;
            }

            // Compilation is expensive, we only want to do it once.
            lock (_compileLock) {
                if (_compiled != null) {
                    return;
                }

                // Interpreter needs a standard delegate type.
                // So change the lambda's delegate type to Func<...> or
                // Action<...> so it can be called from the LightLambda.Run
                // methods.
                LambdaExpression lambda = _lambda;
                if (_interpreter != null) {
                    _compiledDelegateType = GetFuncOrAction(lambda);
                    lambda = Expression.Lambda(_compiledDelegateType, lambda.Body, lambda.Name, lambda.Parameters);
                }

                if (HasClosure) {
                    _compiled = LightLambdaClosureVisitor.BindLambda(lambda, _closureVariables);
                } else {
                    _compiled = lambda.Compile();
                }
            }
        }

        private static Type GetFuncOrAction(LambdaExpression lambda) {
            Type delegateType;
            bool isVoid = lambda.ReturnType == typeof(void);

            if (isVoid && lambda.Parameters.Count == 2 &&
                lambda.Parameters[0].IsByRef && lambda.Parameters[1].IsByRef) {
                return typeof(ActionRef<,>).MakeGenericType(lambda.Parameters.Map(p => p.Type));
            } else {
                Type[] types = lambda.Parameters.Map(p => p.IsByRef ? p.Type.MakeByRefType() : p.Type);
                if (isVoid) {
                    if (Expression.TryGetActionType(types, out delegateType)) {
                        return delegateType;
                    }
                } else {
                    types = types.AddLast(lambda.ReturnType);
                    if (Expression.TryGetFuncType(types, out delegateType)) {
                        return delegateType;
                    }
                }
                return lambda.Type;
            }
        }

        /// <summary>
        /// Updates the execution count of this light delegate. If a certain
        /// threshold is reached, it will start a background compilation.
        /// </summary>
        internal void UpdateExecutionCount() {
            Debug.Assert(_interpreter != null);

            // Don't lock here, it's a frequently hit path.
            //
            // There could be multiple threads racing, but that is okay.
            // Two bad things can happen:
            //   * We miss increments (one thread sets the counter back)
            //   * We might enter the "if" branch more than once.
            //
            // The first is okay, it just means we take longer to compile.
            // The second we explicitly guard against inside of Compile().
            //
            if (++_executionCount >= CompilationThreshold) {
                // Kick off the compile on another thread so this one can keep going
                ThreadPool.QueueUserWorkItem(Compile, null);
            }
        }
    }
}

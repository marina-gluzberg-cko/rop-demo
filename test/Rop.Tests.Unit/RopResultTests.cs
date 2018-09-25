using AutoFixture;
using NSpec;
using Rop;
using SemanticComparison;
using SemanticComparison.Fluent;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoolResult = Rop.Result<bool, string>;
using IntResult = Rop.Result<int, string>;

namespace Klarna.Tests.Unit.Domain
{
    [Tag("rop")]
    public class describe_RopResult : nspec
    {
        private Fixture _fixture = new Fixture();

        void simple_result_creation()
        {
            Result<int, bool> result = null;
            int intValue = default(int);
            bool boolValue = default(bool);

            new Each<string, Func<Result<int, bool>>, bool>
            {
                {
                    "Succeeded",
                    () => Result<int, bool>.Succeeded(intValue = _fixture.Create<int>()),
                    true
                },
                {
                    "Failed",
                    () => Result<int, bool>.Failed(boolValue = _fixture.Create<bool>()),
                    false
                },
            }.Do((name, createResult, isSuccess) =>
            {
                context[$"given {name} result"] = () =>
                {
                    act = () =>
                    {
                        result = createResult();
                    };

                    it[$"IsSuccess should be {isSuccess}"] = () =>
                    {
                        result.IsSuccess.ShouldBe(isSuccess);
                    };

                    it[$"IsFailure should be {!isSuccess}"] = () =>
                    {
                        result.IsFailure.ShouldBe(!isSuccess);
                    };

                    it["Result value should be equal to the expected one"] = () =>
                    {
                        if (isSuccess)
                        {
                            result.Success.ShouldBe(intValue);
                        }
                        else
                        {
                            result.Failure.ShouldBe(boolValue);
                        }
                    };
                };
            });
        }

        void applying_either_on_result()
        {
            IntResult result = null;

            context[$"when either applied on Succeeded result"] = () =>
            {
                IntResult onSuccessResult = null;

                act = () =>
                {
                    result = BoolResult.Succeeded(_fixture.Create<bool>()).Either(
                        x => onSuccessResult = IntResult.Succeeded(_fixture.Create<int>()),
                        x => IntResult.Succeeded(_fixture.Create<int>()));
                };

                it["result should be equal to the on Successful result"] = () => {
                    result.AsSource().OfLikeness<IntResult>().ShouldEqual(onSuccessResult);
                };
            };

            context[$"when either applied on Failed result"] = () =>
            {
                IntResult onFailedResult = null;

                act = () =>
                {
                    result = BoolResult.Failed(_fixture.Create<string>()).Either(
                        x => IntResult.Succeeded(_fixture.Create<int>()),
                        x => onFailedResult = IntResult.Succeeded(_fixture.Create<int>()));
                };

                it["result should be equal to the on Successful result"] = () => {
                    result.AsSource().OfLikeness<IntResult>().ShouldEqual(onFailedResult);
                };
            };
        }

        void forcing_to_failure()
        {
            Result<int, int[]> result = null;

            context[$"when forcing failure on Failed"] = () =>
            {
                int[] failures = null;
           
                act = () =>
                {
                    result = Result<int, int[]>.Failed(failures = _fixture.CreateMany<int>().ToArray()).ToFailure();
                };

                it["result should be equal to the initial result"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int, int[]>.Failed(failures));
                };
            };

            context[$"when forcing failure on Succeeded"] = () =>
            {
                act = () =>
                {
                    result = Result<int, int[]>.Succeeded(_fixture.Create<int>()).ToFailure();
                };

                it["result should be equal to an empty Failed result"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int, int[]>.Failed(new int[0]));
                };
            };
        }

        void merging_results()
        {
            Result<int[], bool[]> result = null;

            context[$"when merging Successes together as a Success"] = () =>
            {
                int[] accumulatdSuccess = default(int[]);
                int nextSuccess = default(int);

                act = () =>
                {
                    result = Result<int[], bool[]>
                                .Succeeded(accumulatdSuccess = _fixture.CreateMany<int>().ToArray())
                                .Merge(Result<int, bool[]>
                                    .Succeeded(nextSuccess = _fixture.Create<int>()));
                };

                it["result should be equal to the Successful accomulation of their Successes"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], bool[]>.Succeeded(accumulatdSuccess.Concat(new[] { nextSuccess }).ToArray()));
                };
            };

            context[$"when merging Success and Failure together as a Failure"] = () =>
            {
                int[] accumulatdSuccess = default(int[]);
                bool[] nextFailures = default(bool[]);

                act = () =>
                {
                    result = Result<int[], bool[]>
                                .Succeeded(accumulatdSuccess = _fixture.CreateMany<int>().ToArray())
                                .Merge(Result<int, bool[]>
                                    .Failed(nextFailures = _fixture.CreateMany<bool>().ToArray()));
                };

                it["result should be equal to the Falied result of merged failures"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], bool[]>.Failed(nextFailures));
                };
            };

            context[$"when merging Failure and Success together as a Failure"] = () =>
            {
                Result<int[], bool[]> initialFailure = null;

                act = () =>
                {
                    result = (initialFailure = Result<int[], bool[]>
                                .Failed(_fixture.CreateMany<bool>().ToArray()))
                                .Merge(Result<int, bool[]>
                                    .Succeeded(_fixture.Create<int>()));
                };

                it["result should be equal to the initial Failure"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(initialFailure);
                };
            };

            context[$"when merging Failures together as a Failure"] = () =>
            {
                bool[] accumulatdFailure = default(bool[]);
                bool[] nextFailures = default(bool[]);

                act = () =>
                {
                    result = Result<int[], bool[]>
                                .Failed(accumulatdFailure = _fixture.CreateMany<bool>().ToArray())
                                .Merge(Result<int, bool[]>
                                    .Failed(nextFailures = _fixture.CreateMany<bool>().ToArray()));
                };

                it["result should be equal to the Failed accomulation of their Failures"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], bool[]>.Failed(accumulatdFailure.Concat(nextFailures).ToArray()));
                };
            };
        }

        void mapping_result()
        {
            Result<bool, string[]> result = null;

            context[$"when mapping a Success (sync)"] = () =>
            {
                bool mappedValue = default(bool);

                act = () =>
                {
                    result = Result<int, string[]>
                                .Succeeded(_fixture.Create<int>())
                                .Map(x => mappedValue = _fixture.Create<bool>());
                };

                it["result should be equal to Successful result with the mapped value"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Succeeded(mappedValue));
                };
            };

            context[$"when mapping a Success (async)"] = () =>
            {
                bool mappedValue = default(bool);

                actAsync = async () =>
                {
                    result = await Task.FromResult(
                                        Result<int, string[]>
                                            .Succeeded(_fixture.Create<int>()))
                                       .Map(x => mappedValue);
                };

                it["result should be equal to Successful result with the mapped value"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Succeeded(mappedValue));
                };
            };

            context[$"when mapping (async) a Success (async)"] = () =>
            {
                bool mappedValue = default(bool);

                actAsync = async () =>
                {
                    result = await Task.FromResult(
                                        Result<int, string[]>
                                            .Succeeded(_fixture.Create<int>()))
                                       .MapAsync(x => Task.FromResult(mappedValue));
                };

                it["result should be equal to Successful result with the mapped value"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Succeeded(mappedValue));
                };
            };

            context[$"when mapping a Failure (sync)"] = () =>
            {
                string[] failureValue = default(string[]);

                act = () =>
                {
                    result = Result<bool, string[]>
                                .Failed(failureValue = _fixture.CreateMany<string>().ToArray())
                                .Map(x => _fixture.Create<bool>());
                };

                it["result should be equal to Failed result with the initial failures"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Failed(failureValue));
                };
            };

            context[$"when mapping a Failure (async)"] = () =>
            {
                string[] failureValue = default(string[]);

                actAsync = async () =>
                {
                    result = await Task.FromResult(
                                        Result<bool, string[]>
                                            .Failed(failureValue = _fixture.CreateMany<string>().ToArray()))
                                       .Map(x => _fixture.Create<bool>());
                };

                it["result should be equal to Failed result with the initial failures"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Failed(failureValue));
                };
            };

            context[$"when mapping (async) a Failure (async)"] = () =>
            {
                string[] failureValue = default(string[]);

                actAsync = async () =>
                {
                    result = await Task.FromResult(
                                        Result<bool, string[]>
                                            .Failed(failureValue = _fixture.CreateMany<string>().ToArray()))
                                       .MapAsync(x => Task.FromResult(_fixture.Create<bool>()));
                };

                it["result should be equal to Failed result with the initial failures"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<bool, string[]>.Failed(failureValue));
                };
            };
        }

        void bind_results()
        {
            int successValue = default(int);
            string failureValue = default(string);
            bool boundSuccessValue = default(bool);
            string boundFailureValue = default(string);

            BoolResult result = null;

            new Each<(string, string), Func<IntResult>, Func<int, BoolResult>, Func<BoolResult>>
            {
                {
                    ("Success bound to Succcess", "bound Success"),
                    () => IntResult.Succeeded(successValue),
                    i => BoolResult.Succeeded(boundSuccessValue),
                    () => BoolResult.Succeeded(boundSuccessValue)
                },
                {
                    ("Success to Failure", "initial Failure"),
                    () => IntResult.Failed(failureValue),
                    i => BoolResult.Succeeded(boundSuccessValue),
                    () => BoolResult.Failed(failureValue)
                },
                {
                    ("Failure to Success", "bound Failure"),
                    () => IntResult.Succeeded(successValue),
                    i => BoolResult.Failed(boundFailureValue),
                    () => BoolResult.Failed(boundFailureValue)
                },
                {
                    ("Failure to Failure", "initial Failure"),
                    () => IntResult.Failed(failureValue),
                    i => BoolResult.Failed(boundFailureValue),
                    () => BoolResult.Failed(failureValue)
                }
            }.Do((text, getInitial, bindMethod, getExpectedResult) =>
            {
                before = () =>
                {
                    successValue = _fixture.Create<int>();
                    failureValue = _fixture.Create<string>();
                    boundSuccessValue = _fixture.Create<bool>();
                    boundFailureValue = _fixture.Create<string>();
                };

                context[$"when binding {text.Item1} (sync)"] = () =>
                {                   
                    act = () =>
                    {
                        result = getInitial().Bind(bindMethod);
                    };


                    it[$"result should be equal to the {text.Item2}"] = () =>
                    {
                        result.AsSource()
                            .OfLikeness<BoolResult>()
                            .ShouldEqual(getExpectedResult());
                    };
                };

                context[$"when binding {text.Item1} (sync to async)"] = () =>
                {
                    actAsync = async () =>
                    {
                        result = await Task.FromResult(getInitial()).Bind(bindMethod);
                    };


                    it[$"result should be equal to the {text.Item2}"] = () =>
                    {
                        result.AsSource()
                            .OfLikeness<BoolResult>()
                            .ShouldEqual(getExpectedResult());
                    };
                };

                context[$"when binding {text.Item1} (async to sync)"] = () =>
                {
                    actAsync = async () =>
                    {
                        result = await getInitial().BindAsync(m => Task.FromResult(bindMethod(m)));
                    };
                    

                    it[$"result should be equal to the {text.Item2}"] = () =>
                    {
                        result.AsSource()
                            .OfLikeness<BoolResult>()
                            .ShouldEqual(getExpectedResult());
                    };
                };

                context[$"when binding {text.Item1} (async to async)"] = () =>
                {
                    actAsync = async () =>
                    {
                        result = await Task.FromResult(getInitial()).BindAsync(m => Task.Run(() => bindMethod(m)));
                    };


                    it[$"result should be equal to the {text.Item2}"] = () =>
                    {
                        result.AsSource()
                            .OfLikeness<BoolResult>()
                            .ShouldEqual(getExpectedResult());
                    };
                };
            });
        }

        void aggregating_results()
        {
            List<int> successValues = null;
            List<string[]> failureValues = null;

            Result<int[], string[]> result = null;

            before = () =>
            {
                successValues = _fixture.CreateMany<int>().ToList();
                failureValues = _fixture.CreateMany<string[]>().ToList();
            };

            context[$"when aggregating empty list of results"] = () =>
            {              
                act = () =>
                {
                    result = new Result<int, string[]>[0].Aggregate();
                };

                it[$"result should be empty Successful"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], string[]>.Succeeded(new int[0]));
                };
            };

            context[$"when aggregating list of Succssful results"] = () =>
            {
                act = () =>
                {
                    result = successValues.Select(i => Result<int, string[]>.Succeeded(i)).Aggregate();
                };

                it[$"result should be Successful aggregation of all of the given results"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], string[]>.Succeeded(successValues.ToArray()));
                };
            };

            context[$"when aggregating list of Successful and Failure results"] = () =>
            {
                act = () =>
                {
                    result = successValues
                                .Select(x => (successValues.IndexOf(x) * 2, Result<int, string[]>.Succeeded(x)))           // Create Success results (with order)
                                .Concat(                                                                                   // ... Concat ...          
                                    failureValues                                                                          // ... Create Failure results ...
                                        .Select(x => (failureValues.IndexOf(x) * 2 + 1, Result<int, string[]>.Failed(x)))) // ... (with order) ...
                                .OrderBy(x => x.Item1)                                                                     // Shuffle order 
                                .Select(x => x.Item2)                                                                      // Keep only the results (without order)                                                                       
                                .Aggregate();                                                                              // Aggregate
                };

                it[$"result should be Failure aggregation of the given Failure results"] = () =>
                {
                    result.AsSource().OfCustomLikeness().ShouldEqual(Result<int[], string[]>.Failed(failureValues.SelectMany(m => m).ToArray()));
                };
            };
        }

        void handling_result()
        {
            bool onSuccessCalled = default(bool);
            bool onFailureCalled = default(bool);

            IntResult succeededResult = null;
            IntResult failedResult = null;

            before = () =>
            {
                onSuccessCalled = false;
                onFailureCalled = false;

                succeededResult = IntResult.Succeeded(_fixture.Create<int>());
                failedResult = IntResult.Failed(_fixture.Create<string>());
            };

            context[$"when handling a Succeeded result (sync)"] = () =>
            {
                act = () => succeededResult
                                .Handle(m => { onSuccessCalled = true; }, m => { });
            
                it["onSuccess should have been called"] = () =>
                {
                    onSuccessCalled.ShouldBeTrue();
                };
            };
        
            context[$"when handling a Succeeded result (async)"] = () =>
            {
                actAsync = async () => await Task.FromResult(succeededResult)
                                    .HandleAsync(m => { onSuccessCalled = true; }, m => { });

                it["onSuccess should have been called"] = () =>
                {
                    onSuccessCalled.ShouldBeTrue();
                };
            };

            context[$"when handling a Failed result (sync)"] = () =>
            {
                act = () => failedResult
                                .Handle(m => { }, m => { onFailureCalled = true; });
            
                it["onFailure method should have been called"] = () =>
                {
                    onFailureCalled.ShouldBeTrue();
                };
            };

            context[$"when handling a failed result (async)"] = () =>
            {
                actAsync = async () => await Task.FromResult(failedResult)
                                    .HandleAsync(m => { }, m => { onFailureCalled = true; });

                it["onFailure method should have been called"] = () =>
                {
                    onFailureCalled.ShouldBeTrue();
                };
            };
        }
    }

        internal static class SourceExtensions
        {
            public static Likeness<Result<TS, TF[]>, Result<TS, TF[]>> OfCustomLikeness<TS, TF>(this LikenessSource<Result<TS, TF[]>> source)
            {
                return source
                        .OfLikeness<Result<TS, TF[]>>()
                        .With(m => m.Failure)
                            .EqualsWhen((x, y) => x.Failure == y.Failure || x.Failure.SequenceEqual(y.Failure));
            }

            public static Likeness<Result<TS[], TF[]>, Result<TS[], TF[]>> OfCustomLikeness<TS, TF>(this LikenessSource<Result<TS[], TF[]>> source)
            {
                return source
                        .OfLikeness<Result<TS[], TF[]>>()
                        .With(m => m.Failure)
                            .EqualsWhen((x, y) => x.Failure == y.Failure || x.Failure.SequenceEqual(y.Failure))
                        .With(m => m.Success)
                            .EqualsWhen((x, y) => x.Success == y.Success || x.Success.SequenceEqual(y.Success));
            }
        }
}
 
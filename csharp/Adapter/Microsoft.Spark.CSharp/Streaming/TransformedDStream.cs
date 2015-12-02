﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Spark.CSharp.Core;
using Microsoft.Spark.CSharp.Proxy;
using Microsoft.Spark.CSharp.Interop;

namespace Microsoft.Spark.CSharp.Streaming
{
    /// <summary>
    /// TransformedDStream is an DStream generated by an C# function
    /// transforming each RDD of an DStream to another RDDs.
    /// 
    /// Multiple continuous transformations of DStream can be combined into
    /// one transformation.
    /// </summary>
    /// <typeparam name="U"></typeparam>
    [Serializable]
    internal class TransformedDStream<U> : DStream<U>
    {
        protected Func<double, RDD<dynamic>, RDD<dynamic>> func;
        private Func<double, RDD<dynamic>, RDD<dynamic>> prevFunc;

        internal void Init<T>(DStream<T> prev, Func<double, RDD<dynamic>, RDD<dynamic>> f)
        {
            streamingContext = prev.streamingContext;
            serializedMode = SerializedMode.Byte;
            isCached = false;
            isCheckpointed = false;
            dstreamProxy = null;

            if (prev is TransformedDStream<T> && !prev.isCached && !prev.isCheckpointed)
            {
                prevFunc = (prev as TransformedDStream<T>).func;
                func = new NewFuncWrapper(f, prevFunc).Execute;
                prevDStreamProxy = prev.prevDStreamProxy;
                prevSerializedMode = prev.prevSerializedMode;
            }
            else
            {
                prevDStreamProxy = prev.dstreamProxy;
                prevSerializedMode = prev.serializedMode;
                func = f;
            }
        }

        [Serializable]
        private class NewFuncWrapper
        {
            private readonly Func<double, RDD<dynamic>, RDD<dynamic>> func;
            private readonly Func<double, RDD<dynamic>, RDD<dynamic>> prevFunc;
            internal NewFuncWrapper(Func<double, RDD<dynamic>, RDD<dynamic>> func, Func<double, RDD<dynamic>, RDD<dynamic>> prevFunc)
            {
                this.func = func;
                this.prevFunc = prevFunc;
            }

            internal RDD<dynamic> Execute(double t, RDD<dynamic> rdd)
            {
                return func(t, prevFunc(t, rdd));
            }
        }

        internal override IDStreamProxy DStreamProxy
        {
            get
            {
                if (dstreamProxy == null)
                {
                    var formatter = new BinaryFormatter();
                    var stream = new MemoryStream();
                    formatter.Serialize(stream, func);
                    dstreamProxy = SparkCLREnvironment.SparkCLRProxy.CreateCSharpDStream(prevDStreamProxy, stream.ToArray(), prevSerializedMode.ToString());
                }
                return dstreamProxy;
            }
        }
    }
}

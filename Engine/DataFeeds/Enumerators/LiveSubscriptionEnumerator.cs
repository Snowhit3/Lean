﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using QuantConnect.Data;
using QuantConnect.Util;
using System.Collections;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Enumerator that will subscribe through the provided data queue handler and refresh the subscription if any mapping occurs
    /// </summary>
    public class LiveSubscriptionEnumerator : IEnumerator<BaseData>
    {
        private readonly Symbol _requestedSymbol;
        private SubscriptionDataConfig _currentConfig;
        private IEnumerator<BaseData> _previousEnumerator;
        private IEnumerator<BaseData> _underlyingEnumerator;

        public BaseData Current => _underlyingEnumerator.Current;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public LiveSubscriptionEnumerator(SubscriptionDataConfig dataConfig, IDataQueueHandler dataQueueHandler, EventHandler handler)
        {
            _requestedSymbol = dataConfig.Symbol;
            // we adjust the requested config symbol before sending it to the data queue handler
            _currentConfig = new SubscriptionDataConfig(dataConfig, symbol: dataConfig.Symbol.GetLiveSubscriptionSymbol());
            _underlyingEnumerator = dataQueueHandler.Subscribe(_currentConfig, handler);

            // for any mapping event we will re subscribe
            dataConfig.NewSymbol += (_, _) =>
            {
                dataQueueHandler.Unsubscribe(_currentConfig);
                _previousEnumerator = _underlyingEnumerator;

                var oldSymbol = _currentConfig.Symbol;
                _currentConfig = new SubscriptionDataConfig(dataConfig, symbol: dataConfig.Symbol.GetLiveSubscriptionSymbol());
                _underlyingEnumerator = dataQueueHandler.Subscribe(_currentConfig, handler);

                Log.Trace($"LiveSubscriptionEnumerator({_requestedSymbol}): " +
                    $"resubscribing old: '{oldSymbol.Value}' new '{_currentConfig.Symbol.Value}'");
            };
        }

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        public bool MoveNext()
        {
            if (_previousEnumerator != null)
            {
                // if previous is set we dispose of it here since we are the consumers of it
                _previousEnumerator.DisposeSafely();
                _previousEnumerator = null;
            }

            var result = _underlyingEnumerator.MoveNext();
            if (Current != null)
            {
                Current.Symbol = _requestedSymbol;
            }

            return result;
        }

        /// <summary>
        /// Reset the IEnumeration
        /// </summary>
        public void Reset()
        {
            _underlyingEnumerator.Reset();
        }

        /// <summary>
        /// Disposes of the used enumerators
        /// </summary>
        public void Dispose()
        {
            _previousEnumerator.DisposeSafely();
            _underlyingEnumerator.DisposeSafely();
        }
    }
}

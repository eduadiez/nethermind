//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Nethermind.Abi;
using Nethermind.Blockchain.Rewards;
using Nethermind.Core;
using Nethermind.Serialization.Json.Abi;

namespace Nethermind.Consensus.AuRa.Contracts
{
    public class RewardContract : SystemContract, IBlockTransitionable
    {
        /// <summary>
        /// produce rewards for the given benefactors,
        /// with corresponding reward codes.
        /// only callable by `SYSTEM_ADDRESS`
        /// function reward(address[] benefactors, uint16[] kind) external returns (address[], uint256[]);
        ///
        /// Kind:
        /// 0 - Author - Reward attributed to the block author
        /// 2 - Empty step - Reward attributed to the author(s) of empty step(s) included in the block (AuthorityRound engine)
        /// 3 - External - Reward attributed by an external protocol (e.g. block reward contract)
        /// 101-106 - Uncle - Reward attributed to uncles, with distance 1 to 6 (Ethash engine)
        /// </summary>
        private const string RewardFunction = "reward";
        
        public long TransitionBlock { get; }
        
        private readonly IAbiEncoder _abiEncoder;
        
        private static readonly AbiDefinition Definition = new AbiDefinitionParser().Parse<RewardContract>();
        
        public static class BenefactorKind
        {
            public const ushort Author = 0;
            public const ushort EmptyStep = 2;
            public const ushort External = 3;
            private const ushort uncleOffset = 100;
            private const ushort minDistance = 1;
            private const ushort maxDistance = 6;

            public static bool TryGetUncle(long distance, out ushort kind)
            {
                if (IsValidDistance(distance))
                {
                    kind = (ushort) (uncleOffset + distance);
                    return true;
                }

                kind = 0;
                return false;
            }

            public static BlockRewardType ToBlockRewardType(ushort kind)
            {
                switch (kind)
                {
                    case Author:
                        return BlockRewardType.Block;
                    case External:
                        return BlockRewardType.External;
                    case EmptyStep:
                        return BlockRewardType.EmptyStep;
                    case ushort uncle when IsValidDistance(uncle - uncleOffset):
                        return BlockRewardType.Uncle;
                    default:
                        throw new ArgumentException($"Invalid BlockRewardType for kind {kind}", nameof(kind));
                }
            }
                
            private static bool IsValidDistance(long distance)
            {
                return distance >= minDistance && distance <= maxDistance;
            }
        }
        
        public RewardContract(IAbiEncoder abiEncoder, Address contractAddress, long transitionBlock) : base(contractAddress)
        {
            TransitionBlock = transitionBlock;
            _abiEncoder = abiEncoder ?? throw new ArgumentNullException(nameof(abiEncoder));
        }
        
        /// <summary>
        /// produce rewards for the given benefactors,
        /// with corresponding reward codes.
        /// only callable by `SYSTEM_ADDRESS`
        /// function reward(address[] benefactors, uint16[] kind) external returns (address[], uint256[]);
        /// </summary>
        /// <param name="benefactors">benefactor addresses</param>
        /// <param name="kind">
        /// Kind:
        /// 0 - Author - Reward attributed to the block author
        /// 2 - Empty step - Reward attributed to the author(s) of empty step(s) included in the block (AuthorityRound engine)
        /// 3 - External - Reward attributed by an external protocol (e.g. block reward contract)
        /// 101-106 - Uncle - Reward attributed to uncles, with distance 1 to 6 (Ethash engine)
        /// </param>
        public Transaction Reward(Address[] benefactors, ushort[] kind)
            => GenerateSystemTransaction(_abiEncoder.Encode(Definition.Functions[RewardFunction].GetCallInfo(), benefactors, kind));
        
        public (Address[] Addresses, BigInteger[] Rewards) DecodeRewards(byte[] data)
        {
            var objects = _abiEncoder.Decode(Definition.Functions[RewardFunction].GetReturnInfo(), data);
            return ((Address[]) objects[0], (BigInteger[]) objects[1]);
        }
    }
}
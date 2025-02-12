﻿// Copyright (c) Zhongkai Fu. All rights reserved.
// https://github.com/zhongkaifu/Seq2SeqSharp
//
// This file is part of Seq2SeqSharp.
//
// Seq2SeqSharp is licensed under the BSD-3-Clause license found in the LICENSE file in the root directory of this source tree.
//
// Seq2SeqSharp is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the BSD-3-Clause License for more details.

using System;
using System.Collections.Generic;

using AdvUtils;
using Seq2SeqSharp.Applications;
using Seq2SeqSharp.Utils;

namespace Seq2SeqSharp.Models
{
    [Serializable]
    public abstract class Model : IModel
    {
        public int DecoderEmbeddingDim { get; set; }
        public int EncoderEmbeddingDim { get; set; }
        public int DecoderLayerDepth { get; set; }
        public int EncoderLayerDepth { get; set; }

        public ActivateFuncEnums ActivateFunc { get; set; }

        public int ExpertNum { get; set; }
        public int ExpertsPerTokenFactor { get; set; }
        public DecoderTypeEnums DecoderType { get; set; }
        public EncoderTypeEnums EncoderType { get; set; }
        public int HiddenDim { get; set; }
        public bool EnableSegmentEmbeddings { get; set; }
        public int MultiHeadNum { get; set; }
        public Vocab SrcVocab { get; set; }
        public Vocab TgtVocab { get; set; }
        public List<Vocab> ClsVocabs { get; set; }
        public bool EnableCoverageModel { get; set; }
        public bool SharedEmbeddings { get; set; }

        public string SimilarityType { get; set; }

        public bool EnableTagEmbeddings { get; set; }

        public int MaxSegmentNum { get; set; }

        public bool PointerGenerator { get; set; }

        public Vocab ClsVocab
        {
            get
            {
                if (ClsVocabs == null)
                {
                    ClsVocabs = new List<Vocab>
                    {
                        new Vocab()
                    };
                }

                return ClsVocabs[0];
            }

            set
            {
                if (ClsVocabs == null)
                {
                    ClsVocabs = new List<Vocab>
                    {
                        new Vocab()
                    };
                }

                ClsVocabs[0] = value;
            }
        }

        public Dictionary<string, float[]> Name2Weights { get; set; }

        public int VQSize { get; set; }
        public Dictionary<string, byte[]> Name2WeightsVQ { get; set; }       
        public Dictionary<string, double[]> Name2CodeBook { get; set; }

        public Model() { }
        public Model(Options opts,Vocab srcVocab)
        {
            HiddenDim = opts.HiddenSize;
            EncoderLayerDepth = opts.EncoderLayerDepth;;
            EncoderType = opts.EncoderType;
            MultiHeadNum = opts.MultiHeadNum;
            SrcVocab = srcVocab;
            EncoderEmbeddingDim = opts.SrcEmbeddingDim;
            EnableSegmentEmbeddings = opts.EnableSegmentEmbeddings;
            EnableTagEmbeddings = opts.EnableTagEmbeddings;
            MaxSegmentNum = opts.MaxSegmentNum;
            ExpertNum = opts.ExpertNum;
            ExpertsPerTokenFactor = opts.ExpertsPerTokenFactor;
            ActivateFunc = opts.ActivateFunc;
            VQSize = opts.VQSize;

            Name2Weights = new Dictionary<string, float[]>();
            Name2WeightsVQ = new Dictionary<string, byte[]>();
            Name2CodeBook = new Dictionary<string, double[]>();
        }

        public Model(Model_4_ProtoBufSerializer m)
        {
            HiddenDim = m.HiddenDim;
            EncoderLayerDepth = m.EncoderLayerDepth; ;
            EncoderType = m.EncoderType;
            MultiHeadNum = m.MultiHeadNum;
            SrcVocab = m.SrcVocab?.ToVocab();
            EncoderEmbeddingDim = m.EncoderEmbeddingDim;
            EnableSegmentEmbeddings = m.EnableSegmentEmbeddings;
            EnableTagEmbeddings = m.EnableTagEmbeddings;
            MaxSegmentNum = m.MaxSegmentNum;
            ExpertNum = m.ExpertNum;
            ExpertsPerTokenFactor = m.ExpertsPerTokenFactor;
            SimilarityType = m.SimilarityType;
            ActivateFunc = m.ActivateFunc;
            VQSize = m.VQSize;

            Name2Weights = m.Name2Weights;
            Name2WeightsVQ = m.Name2WeightsVQ;
            Name2CodeBook = m.Name2CodeBook;

            if (Name2Weights == null)
            {
                Name2Weights = new Dictionary<string, float[]>();
            }

            if (Name2WeightsVQ == null)
            {
                Name2WeightsVQ = new Dictionary<string, byte[]>();
            }

            if (Name2CodeBook == null)
            {
                Name2CodeBook = new Dictionary<string, double[]>();
            }
        }

        public void AddWeights(string name, float[] weights)
        {
            Logger.WriteLine($"Adding weights '{name}' to the model.");

            if (VQSize > 0)
            {
                VectorQuantization vq = new VectorQuantization();
                foreach (var v in weights)
                {
                    vq.Add(v);
                }

                for (int i = weights.Length; i < VQSize; i++)
                {
                    vq.Add(0);
                }

                double distortion = vq.BuildCodebook(VQSize);

                Name2CodeBook.Add(name, vq.CodeBook);

                byte[] bweights = new byte[weights.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    bweights[i] = (byte)vq.ComputeVQ(weights[i]);
                }

                Name2WeightsVQ.Add(name, bweights);
            }
            else
            {
                Name2Weights.Add(name, weights);
            }
        }

        public float[] GetWeights(string name)
        {
            float[] weight = null;
            if (VQSize > 0)
            {
                if (Name2WeightsVQ.ContainsKey(name) == false)
                {
                    Logger.WriteLine(Logger.Level.warn, ConsoleColor.Yellow, $"Weight '{name}' doesn't exist in the model.");
                    return null;
                }

                var codeBook = Name2CodeBook[name];

                weight = new float[Name2WeightsVQ[name].Length];
                for (int i = 0; i < Name2WeightsVQ[name].Length; i++)
                {
                    weight[i] = (float)codeBook[Name2WeightsVQ[name][i]];
                }
            }
            else
            {
                if (Name2Weights.ContainsKey(name) == false)
                {
                    Logger.WriteLine(Logger.Level.warn, ConsoleColor.Yellow, $"Weight '{name}' doesn't exist in the model.");
                    return null;
                }

                weight = Name2Weights[name];
            }

            return weight;
        }

        public void DeleteWeights(string name)
        {
            if (VQSize > 0)
            {
                Name2WeightsVQ.Remove(name);
                Name2CodeBook.Remove(name);
            }
            else
            {
                Name2Weights.Remove(name);
            }
        }

        public void ClearWeights()
        {
            Name2WeightsVQ.Clear();
            Name2CodeBook.Clear();
            Name2Weights.Clear();
        }

        public void ShowModelInfo()
        {
            Logger.WriteLine($"Encoder embedding dim: '{EncoderEmbeddingDim}'");
            Logger.WriteLine($"Decoder embedding dim: '{DecoderEmbeddingDim}'");
            Logger.WriteLine($"Encoder layer depth: '{EncoderLayerDepth}'");
            Logger.WriteLine($"Decoder layer depth: '{DecoderLayerDepth}'");
            Logger.WriteLine($"Encoder type: '{EncoderType}'");
            Logger.WriteLine($"Decoder type: '{DecoderType}'");
            Logger.WriteLine($"Hidden layer dim: '{HiddenDim}'");
            Logger.WriteLine($"Enable segment embeddings: '{EnableSegmentEmbeddings}'");
            Logger.WriteLine($"Enable shared embeddings: '{SharedEmbeddings}'");
            Logger.WriteLine($"Enable tag embeddings: '{EnableTagEmbeddings}'");
            Logger.WriteLine($"Multi-head size: '{MultiHeadNum}'");
            Logger.WriteLine($"Pointer Generator: '{PointerGenerator}'");
            Logger.WriteLine($"Expert Size: '{ExpertNum}");
            Logger.WriteLine($"Experts per token factor: '{ExpertsPerTokenFactor}'");
            Logger.WriteLine($"Codebook size for model vector quantization: '{VQSize}'");


            if (!SimilarityType.IsNullOrEmpty())
            {
                Logger.WriteLine($"Similarity Type: '{SimilarityType}'");
            }

            if (SrcVocab != null)
            {
                Logger.WriteLine($"Source vocabulary size: '{SrcVocab.Count}'");
            }

            if (TgtVocab != null)
            {
                Logger.WriteLine($"Target vocabulary size: '{TgtVocab.Count}'");
            }

            if (ClsVocabs != null)
            {
                Logger.WriteLine($"The number of CLS vocabularies: '{ClsVocabs.Count}' ");
                for (int i = 0; i < ClsVocabs.Count; i++)
                {
                    Logger.WriteLine($"CLS vocabulary {i} size: {ClsVocabs[i].Count}");
                }
            }
        }
    }
}

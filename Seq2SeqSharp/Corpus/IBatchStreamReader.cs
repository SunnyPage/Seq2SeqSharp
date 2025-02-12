﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seq2SeqSharp.Corpus
{
    public interface IBatchStreamReader<T> where T : ISntPairBatch, new()
    {
        (int, ISntPairBatch) GetNextBatch();
    }
}

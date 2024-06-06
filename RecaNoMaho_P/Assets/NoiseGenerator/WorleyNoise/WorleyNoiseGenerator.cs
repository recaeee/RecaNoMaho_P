using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WorleyNoiseGenerator : BaseNoise {
    public enum ReturnType {
        Cell = 0,
        IrregularCell = 1,
        Rock = 2,
        IrregularRock = 3,
    }

    [Space]
    [Space]
    [Space]
    public ReturnType returnType;

    public override void Generate() {
        cs_core.SetInt("_ReturnType", returnType.GetHashCode());

        base.Generate();
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp2.Models;
public class SensorData
{
    public double SuhuLampu
    {
        get; set;
    }
    public double Kelembapan
    {
        get; set;
    }
    public bool StatusLampu
    {
        get; set;
    }
    public bool StatusKipas
    {
        get; set;
    }

    public float Threshold
    {
        get; set;
    }
    public bool ManualModeLamp
    {
        get; set;
    } // Tambahan untuk mode manual
    public bool ManualModeFan
    {
        get; set;
    }  // Tambahan untuk mode manual
}


using System;
using System.Collections;
using System.Collections.Generic;
using OctavianLib;

namespace PichaLib
{
    public static class PFactory
    {
        private static Random _Random = new Random();

        public static SortedList<int, Chroma[,]> ProcessLayer(Layer l)
        {
            return PFactory._GenColors(PFactory._GenShape(l), l);
        }

        private static SortedList<int, string[,]> _GenShape(Layer l)
        {
            var _output = new SortedList<int, string[,]>();

            foreach(KeyValuePair<int, string[,]> _frame in l.Frames)
            {
                string[,] _frameCopy = (string[,])_frame.Value.Clone();

                foreach(KeyValuePair<int, Cycle> _cycle in l.Cycles)
                {
                    _frameCopy = PFactory._RunCycle(_frameCopy, _cycle.Value.Policies);
                }
                _output.Add(_frame.Key, _frameCopy);
            }
            return _output;
        }

        private static SortedList<int, Chroma[,]> _GenColors(SortedList<int, string[,]> cells, Layer l)
        {
            var _output = new SortedList<int, Chroma[,]>();
            var _cellColors = new Dictionary<string, CellData>();

            foreach(Pixel _type in l.Pixels.Values)
            {
                CellData _dat = new CellData();
                
                if(_type.RandomCol)
                {
                    _dat.RGB = new Chroma(
                        (float)PFactory._Random.NextDouble(),
                        (float)PFactory._Random.NextDouble(),
                        (float)PFactory._Random.NextDouble());
                }
                else
                    { _dat.RGB = _type.Color; }

                _dat.HSL = _dat.RGB.ToHSL();
                _dat.Sat = (float)PFactory._Random.RandfRange(_type.MinSaturation * _dat.HSL.s, _dat.HSL.s);


                _cellColors.Add(_type.Name, _dat);
            }

            foreach(KeyValuePair<int, string[,]> _pair in cells)
            {
                int _w = _pair.Value.GetWidth();
                int _h = _pair.Value.GetHeight();
                var _frameOut = new Chroma[_h, _w];

                for(int _y = 0; _y < _h; _y++)
                {
                    for(int _x = 0; _x < _w; _x++)
                    {
                        var _cell = _pair.Value[_y, _x];
                        CellData _cSet;
                        if(_cell != Pixel.NULL)
                        {
                            _cSet = _cellColors[_cell];float _grade = 0f;

                            switch(l.Pixels[_cell].FadeDirection)
                            {
                                case FadeDirection.NORTH:
                                    _grade = (float)((_y + 1f) / _h);
                                    break;
                                case FadeDirection.WEST:
                                    _grade = (float)((_x + 1f) / _w);
                                    break;
                                case FadeDirection.SOUTH:
                                    _grade = 1f - (float)((_y + 1f) / _h);
                                    break;
                                case FadeDirection.EAST:
                                    _grade = 1f - (float)((_x + 1f) / _w);
                                    break;
                                case FadeDirection.NONE:
                                    _grade = 1f;
                                    break;
                            }

                            float u_sin = (float)Math.Cos(_grade * Math.PI);
                            float _l = (float)(PFactory._Random.RandfRange(0f, l.Pixels[_cell].BrightNoise) * u_sin) + _cSet.HSL.l;

                            _frameOut[_y, _x] = Chroma.CreateFromHSL(_cSet.HSL.h, _cSet.Sat, _l, _cSet.HSL.a);
                        }
                        else
                        {
                            // is the cell is null just fill with transparent pixel.
                            _frameOut[_y, _x] = Chroma.CreateFromBytes(0, 0, 0, 0);
                        }
                    }
                }

                if(l.MirrorX) { _frameOut = _frameOut.MirrorX(); }
                if(l.MirrorY) { _frameOut = _frameOut.MirrorY(); }

                _output.Add(_pair.Key, _frameOut);
            }


            return _output;
        }

        private static string[,] _RunCycle(string[,] cells, List<Policy> cycle)
        {
            (int W, int H) _size = (cells.GetWidth(), cells.GetHeight());

            var _output = new string[_size.H, _size.W];

            for(int _x = 0; _x < _size.W; _x++)
            {
                for(int _y = 0; _y < _size.H; _y++)
                {
                    string _cType = cells[_y, _x];
                    var _policies = cycle.FindAll(p => p.Input == _cType);
                    if(_policies.Count > 0)
                    { 
                        foreach(Policy _p in _policies)
                        {
                            _output[_y, _x] = cells.RunPolicy(_p, _x, _y);
                        }
                    }
                    else
                        { _output[_y, _x] = _cType; }
                }
            }
            
            return _output;
        }

        private static string RunPolicy(this string[,] array, Policy p, int x, int y)
        {
            if(PFactory._Random.NextDouble() <= p.Rate)
            {
                if(p.ConditionA != ConditionTarget.NONE && p.ConditionLogic != ConditionExpression.NONE)
                {
                    switch(p.ConditionA)
                    {
                        case ConditionTarget.NEIGHBOR:
                            if(p.ConditionLogic == ConditionExpression.IS &&
                               array.NeighborIs(x, y, p.ConditionB))
                                { return p.Output; }
                            else if(p.ConditionLogic == ConditionExpression.IS_NOT &&
                                    array.NeighborIsNot(x, y, p.ConditionB))
                                { return p.Output; }
                            else
                                { return array[y, x]; }
                        default:
                            return array[y, x];
                    }
                }
                else 
                    { return p.Output; }
            }
            else
                { return array[y, x]; }
        }

        private static bool NeighborIs(this string[,] array, int x, int y, string value) 
        {
            var _testVals = array.GatherNeighbors(x, y);
            foreach(string _v in _testVals) { if(_v == value) { return true; } }
            return false;
        }

        private static bool NeighborIsNot(this string[,] array, int x, int y, string value) 
        {
            var _testVals = array.GatherNeighbors(x, y);
            foreach(string _v in _testVals) { if(_v != value) { return true; } }
            return false;
        }

        private static List<string> GatherNeighbors(this string[,] array, int x, int y)
        {
            List<string> _output = new List<string>();
            List<(int xT, int yT)> _addresses = new List<(int xT, int yT)>() {
                (x - 1, y),
                (x + 1, y),
                (x, y - 1),
                (x, y + 1),
            };

            foreach((int xT, int yT) _a in _addresses)
            {
                try { _output.Add(array[_a.yT, _a.xT]); }
                catch {}
            }

            return _output;
        }
    }

    public struct CellData
    {
        public Chroma RGB;
        public (float h, float s, float l, float a) HSL;
        public float Sat;
    }
}
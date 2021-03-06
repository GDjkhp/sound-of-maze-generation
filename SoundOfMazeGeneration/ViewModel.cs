﻿using NAudio.Wave;
using SoundOfMazeGeneration.Generators;
using SoundOfMazeGeneration.Models;
using SoundOfMazeGeneration.Sound;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SoundOfMazeGeneration
{
    public class ViewModel : INotifyPropertyChanged
    {
        private SineWaveProvider32 _sineWaveProvider = new SineWaveProvider32();
        private List<IMazeGenerator> _generators;
        public ICommand GenerateCommand { get; set; }
        private int _currentGeneratorIndex;
        private Maze _maze;
        private AsioOut _asio;
       // private WaveOut waveOut;
        private IMazeGenerator _generator;
        private State _state;
        private readonly double CELL_SIZE = (Double)Application.Current.Resources["CellSize"];
        private readonly double BORDER_THICKNESS = (Double)Application.Current.Resources["BorderThickness"];
        private int _stepCount;
        public Maze Maze
        {
            get { return _maze; }
            set
            {
                if (_maze == value) return;
                _maze = value;
                NotifyPropertyChanged();
            }
        }

        public IMazeGenerator Generator
        {
            get { return _generator; }
            set
            {
                if (_generator == value) return;
                _generator = value;
                NotifyPropertyChanged();
            }
        }

        public int StepCount
        {
            get { return _stepCount; }
            set
            {
                if (_stepCount == value) return;
                _stepCount = value;
                NotifyPropertyChanged();
            }
        }

        public double CanvasCellSize
        {
            get { return CELL_SIZE + BORDER_THICKNESS * 2; }
        }

        public ViewModel(int rows, int cols)
        {
             Maze = new Maze(rows, cols);
            _generators = new List<IMazeGenerator>()
            {
                new DepthFirstSearchGenerator(Maze),
                new BinaryTreeGenerator(Maze),
                new KruskalsRandomizedGenerator(Maze),
                new SidewinderGenerator(Maze),
                new HuntAndKillGenerator(Maze),
                new PrimsRandomizedGenerator(Maze),
                new EllersGenerator(Maze),
            };

            _asio = new AsioOut("Focusrite USB ASIO");
            _asio.Init(_sineWaveProvider);
            //waveOut = new WaveOut();
            //waveOut.Init(_sineWaveProvider);

            GenerateCommand = new RelayCommand(o =>
            {
                _sineWaveProvider.Frequency = 0;
                _asio.Play();
                //waveOut.Play();

                RunGenerator();
            });
        }

        private void RunGenerator()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1)};
            List<Cell>.Enumerator enumerator = new List<Cell>.Enumerator();
            _state = State.Initializing;
            timer.Start();
            timer.Tick += (sender, args) =>
            {
                if (_state == State.Initializing)
                {
                    Generator = _generators[_currentGeneratorIndex];
                    StepCount = 0;
                    timer.Interval = TimeSpan.FromMilliseconds(Generator.RecommendedTimeStep);
                    _state = State.Running;
                }
                if (_state == State.Running)
                {
                    var currentCell = Generator.NextStep();
                    if (currentCell == null)
                    {
                        _state = State.StartReset;
                        timer.Interval = TimeSpan.FromSeconds(1);
                        _sineWaveProvider.Frequency = 0;
                    }
                    else
                    {
                        _sineWaveProvider.Frequency = CellToFrequency(currentCell);
                        StepCount++;
                    }
                } else if (_state == State.StartReset)
                {
                        timer.Interval = TimeSpan.FromMilliseconds(0.5);
                    Generator.Steps.Reverse();
                        enumerator = Generator.Steps.GetEnumerator();
                    _state = State.Resetting;
                } else if (_state == State.Resetting)
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.CellState == CellState.Unvisited) continue;

                        _sineWaveProvider.Frequency = CellToFrequency(enumerator.Current);
                        enumerator.Current.CellState = CellState.Unvisited;
                        enumerator.Current.Walls = Direction.East | Direction.North | Direction.South | Direction.West;
                        break;
                    }
                    if (enumerator.Current == null)
                    {
                        _state = State.FinishedReset;
                    }
                } else if (_state == State.FinishedReset)
                {
                    _sineWaveProvider.Frequency = 0;
                    timer.Interval = TimeSpan.FromSeconds(1);
                    _currentGeneratorIndex++;
                    _state = _currentGeneratorIndex == _generators.Count ? State.End : State.Initializing;
                } else if (_state == State.End)
                {
                    _asio.Stop();
                    //waveOut.Stop();
                    timer.Stop();
                }
            };
        }

        private float CellToFrequency(Cell cell)
        {
            var distance = Math.Sqrt(Math.Pow(cell.Row, 2) + Math.Pow(cell.Col, 2));
            var total = Math.Sqrt(Math.Pow(_maze.Rows, 2) + Math.Pow(_maze.Cols, 2));
            var freq = Tones.CalculateFrequency(distance, total);
            return (float)freq;
        }

        private enum State
        {
            Initializing,
            Running,
            StartReset,
            Resetting,
            FinishedReset,
            End
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}

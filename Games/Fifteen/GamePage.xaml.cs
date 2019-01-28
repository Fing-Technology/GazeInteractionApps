//Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
//See LICENSE in the project root for license information.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Toolkit.Uwp.Input.GazeInteraction;
using Windows.Foundation.Collections;
using Windows.Foundation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace Fifteen
{
    public sealed partial class GamePage : Page
    {
        int _boardSize = 4;

        Button[,] _buttons;
        int _blankRow;
        int _blankCol;
        int _numMoves;
        bool _interactionPaused = false;

        SolidColorBrush _solidTileBrush = new SolidColorBrush(Colors.Silver);
        SolidColorBrush _blankTileBrush = new SolidColorBrush(Colors.White);
        SolidColorBrush _pausedButtonBrush = new SolidColorBrush(Colors.Black);

        public GamePage()
        {
            InitializeComponent();

            var sharedSettings = new ValueSet();
            GazeSettingsHelper.RetrieveSharedSettings(sharedSettings).Completed = new AsyncActionCompletedHandler((asyncInfo, asyncStatus) =>
            {
                var gazePointer = GazeInput.GetGazePointer(this);
                gazePointer.LoadSettings(sharedSettings);
            });

            Loaded += GamePage_Loaded;
        }

        private void GamePage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeButtonArray();
            while (IsSolved())
            {
                ResetBoard();
            }
            GazeInput.DwellFeedbackCompleteBrush = new SolidColorBrush(Colors.Transparent);
            Button blankBtn = _buttons[_blankRow, _blankCol];
            blankBtn.Background = _blankTileBrush;
            blankBtn.Visibility = Visibility.Visible;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _boardSize = (int)e.Parameter;
        }

        void InitializeButtonArray()
        {
            GameGrid.Children.Clear();
            GameGrid.RowDefinitions.Clear();
            GameGrid.ColumnDefinitions.Clear();

            for (int row = 0; row < _boardSize; row++)
            {
                GameGrid.RowDefinitions.Add(new RowDefinition());
                GameGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            _buttons = new Button[_boardSize, _boardSize];

            for (int row = 0; row < _boardSize; row++)
            {
                for (int col = 0; col < _boardSize; col++)
                {
                    var button = new Button();
                    button.Name = "button" + "_" + col + "_" + row;
                    button.Content = ((row * _boardSize) + col + 1).ToString();
                    button.Tag = (row * _boardSize) + col;
                    button.Click += OnButtonClick;
                    button.Style = Resources["ButtonStyle"] as Style;

                    _buttons[row, col] = button; ;

                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);
                    GameGrid.Children.Add(button);
                }
            }

            GameGrid.UpdateLayout();
        }

        void ResetBoard()
        {
            for (int i = 0; i < _boardSize; i++)
            {
                for (int j = 0; j < _boardSize; j++)
                {
                    _buttons[i, j].Content = ((i * _boardSize) + j + 1).ToString();
                }
            }

            _buttons[_boardSize - 1, _boardSize - 1].Content = "";
            _blankRow = _boardSize - 1;
            _blankCol = _boardSize - 1;

            int shuffleCount = 500;
            Random rnd = new Random();
            while (shuffleCount > 0)
            {
                bool changeRow = rnd.Next(0, 2) == 0;
                bool decrement = rnd.Next(0, 2) == 0;

                int row = _blankRow;
                int col = _blankCol;
                if (changeRow)
                {
                    row = decrement ? row - 1 : row + 1;
                }
                else
                {
                    col = decrement ? col - 1 : col + 1;
                }

                if ((row < 0) || (row >= _boardSize) || (col < 0) || (col >= _boardSize))
                {
                    continue;
                }

                if (SwapBlank(row, col))
                {
                    shuffleCount--;
                }
            }
        }

        bool SwapBlank(int row, int col)
        {
            //Prevent tile slides once puzzle is solved
            if (DialogGrid.Visibility == Visibility.Visible)
            {
                return false;
            }

            if (!((((row == _blankRow - 1) || (row == _blankRow + 1)) && (col == _blankCol)) ||
                 (((col == _blankCol - 1) || (col == _blankCol + 1)) && (row == _blankRow))))
            {
                return false;
            }

            //Slide button visual
            Button btn = _buttons[row, col];
            Button blankBtn = _buttons[_blankRow, _blankCol];

            //Get Visuals for the selected button that is going to appear to slide and for the blank button
            var btnVisual = ElementCompositionPreview.GetElementVisual(btn);
            var compositor = btnVisual.Compositor;
            var blankBtnVisual = ElementCompositionPreview.GetElementVisual(blankBtn);

            var easing = compositor.CreateLinearEasingFunction();

            //Create an animation to first move the blank button with its updated contents to
            //instantly appear in the position position of the selected button
            //then slide that button back into its original position
            var slideAnimation = compositor.CreateVector3KeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0f, btnVisual.Offset);
            slideAnimation.InsertKeyFrame(1f, blankBtnVisual.Offset, easing);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(500);

            //Apply the slide anitmation to the blank button
            blankBtnVisual.StartAnimation(nameof(btnVisual.Offset), slideAnimation);

            //Swap content of the selected button with the blank button and clear the selected button
            _buttons[_blankRow, _blankCol].Content = _buttons[row, col].Content;
            _buttons[row, col].Content = "";
            _blankRow = row;
            _blankCol = col;


            //Note there is some redunancy in the following settings that corrects the UI at board load as well as tile slide 
            //Force selected button to the bottom and the blank button to the top
            Canvas.SetZIndex(btn, -_boardSize);
            Canvas.SetZIndex(blankBtn, 0);

            //Update the background colors of the two buttons to reflect their new condition
            btn.Background = _blankTileBrush;
            blankBtn.Background = _solidTileBrush;

            //Update the visibility to collapse the selected button that is now blank
            btn.Visibility = Visibility.Collapsed;
            blankBtn.Visibility = Visibility.Visible;

            //Disable eye control for the new empty button so that there are no inappropriate dwell indicators
            GazeInput.SetInteraction(blankBtn, Interaction.Inherited);
            GazeInput.SetInteraction(btn, Interaction.Disabled);

            return true;
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int cellNumber = int.Parse(button.Tag.ToString());
            int row = cellNumber / _boardSize;
            int col = cellNumber % _boardSize;

            if (SwapBlank(row, col))
            {
                _numMoves++;
                CheckCompletion();
            }
        }

        bool IsSolved()
        {
            for (int i = 0; i < _boardSize * _boardSize - 1; i++)
            {
                int row = i / _boardSize;
                int col = i % _boardSize;
                if (_buttons[row, col].Content.ToString() != (i + 1).ToString())
                {
                    return false;
                }
            }
            return true;
        }

        void CheckCompletion()
        {
            if (!IsSolved())
            {
                return;
            }

            string message = $"Congratulations!! You solved it in {_numMoves} moves";
            DialogText.Text = message;
            DialogGrid.Visibility = Visibility.Visible;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            //ResetBoard();
            DialogGrid.Visibility = Visibility.Collapsed;

            Frame.Navigate(typeof(MainPage));
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (_interactionPaused)
            {
                button.Content = "\uE769";
                button.Foreground = _pausedButtonBrush;
                button.Background = _solidTileBrush;
                GazeInput.SetInteraction(GameGrid, Interaction.Enabled);
                _interactionPaused = false;
            }
            else
            {
                button.Content = "\uE768";
                button.Foreground = _solidTileBrush;
                button.Background = _pausedButtonBrush;
                GazeInput.SetInteraction(GameGrid, Interaction.Disabled);
                _interactionPaused = true;
            }
        }

        #region
        private int NextTileToSolve()
        {
            int lastSolved = 0;
            for (int row = 0; row < _buttons.GetLength(0); row++)
            {
                for (int col = 0; col < _buttons.GetLength(1); col++)
                {
                    if (_buttons[row, col].Content.ToString() == ((row * _boardSize) + col + 1).ToString())
                    {
                        lastSolved++;
                    }
                    //else if (col % _boardSize == _boardSize - 3 && row < _buttons.GetLength(0)-1 && lastSolved > 0) //note offset is 2 cells over -1 for 0 based index
                    else if (col % _boardSize == _boardSize - 3 && row < _buttons.GetLength(0) - 1) //note offset is 2 cells over -1 for 0 based index // this one worked before I experimented with subpattern
                    //else if (col >= _boardSize - 3 && row < _buttons.GetLength(0) - 1) //note offset is 2 cells over -1 for 0 based index
                    //else if (col <= _boardSize-3 && row < _buttons.GetLength(0) - 1) //note offset is 2 cells over -1 for 0 based index
                    {
                        //Check for the 3 valid broken states

                        
                        Button currentCell = _buttons[row, col];
                        Button cellBelow = _buttons[row + 1, col];
                        Button cellBeside = _buttons[row, col + 1];
                        Button cellOnEndOfRow = _buttons[row, col + 2];
                        Button cellUnderEndOfRow = _buttons[row + 1, col + 2];
                        Button cellUnderBeside = _buttons[row + 1, col + 1];

                        if (currentCell.Content.ToString() == "" && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellOnEndOfRow.Content.ToString() != (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//State one (current row/col is blank and cell below = current.tag and cellBeside.content = cellbeside.tag  and cellOnend.content != (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1)
                            return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == "" && cellOnEndOfRow.Content.ToString() != (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString() && (cellUnderEndOfRow.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString() || cellUnderBeside.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString()))
                        {//State two (current row/col = tag of right neighbor and cell below = current.tag and neighbor = blank and cellOnend.content != (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1)
                            return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() != "" && cellOnEndOfRow.Content.ToString() == "")
                        {//State three (current row/col = tag of right neighbor and cell below = current.tag and neighbor != blank and end is blank
                            return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() != "" && cellOnEndOfRow.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//State four (as above except last condition == cellOnEndofRow.tag.tostring() + 1 instead of blank
                             return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if(currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == "" && cellOnEndOfRow.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//state five (as above except second last condition should be cellBeside.Content.ToString() == ""
                             return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == "" && cellOnEndOfRow.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//state six (current row/col = tag of right neighbor and cell below = current.tag and neighbor = blank and cellOnend.content == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1)
                             return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString() && cellOnEndOfRow.Content.ToString() != (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//state six B(diagnal solve) (current row/col = tag of right neighbor and cell below = current.tag and neighbor = blank and cellOnend.content == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1)
                            return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }
                        else if (currentCell.Content.ToString() == "" && cellBelow.Content.ToString() == (int.Parse(currentCell.Tag.ToString()) + 1).ToString() && cellBeside.Content.ToString() == (int.Parse(cellBeside.Tag.ToString()) + 1).ToString() && cellOnEndOfRow.Content.ToString() == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1).ToString())
                        {//state seven current row/col is blank and cell below = current.tag and cellBeside.content = cellbeside.tag and cellOnend.content == (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1)
                            return (int.Parse(cellOnEndOfRow.Tag.ToString()) + 1);
                        }





                        return lastSolved + 1;
                    }
                    else
                    {
                        return lastSolved + 1;
                    }
                }
            }
            return lastSolved + 1;
        }

        private bool IsTileProtectedForBreak(Button tile, Button targetTile, Button sourceTile)
        {
            // add logic to check for step 1, step 2 protected blocks and add calls to this function to the appropriate places similar to IsTileSolved tests
            // see NextTileToSolve() routine above for the step test condtions
            //NEEDS WORK, false triggers right now, may need to consider NextTileToSolve value in the conditions
            Button blankTile = FindBlankButton();
            int tileIndex = int.Parse(tile.Content.ToString());
            int tryingToSolve = NextTileToSolve();
            Button tryingToSolveButton = FindButtonWithCurrentValue(tryingToSolve);

            int tryingToSolveRow = Grid.GetRow(tryingToSolveButton);
            int tryingToSolveColumn = Grid.GetColumn(tryingToSolveButton);

            if (tryingToSolveColumn % _boardSize == _boardSize - 3 && tryingToSolveRow < _buttons.GetLength(0) - 1)
            {
                //Protect any of the buttons that might be part of the 3 steps of controlled break
                if (int.Parse(tile.Content.ToString()) <= tryingToSolve)
                {
                    return true;
                }

            }

            if (int.Parse(tile.Content.ToString()) <= tryingToSolve)
            {            
                if (tileIndex % _boardSize == 0 && TilesAreAdjcent(targetTile, sourceTile) && blankTile != targetTile)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsTileSolved(Button tile)
        {
           
            int tileIndex = int.Parse(tile.Tag.ToString());
            if (tileIndex < NextTileToSolve())
            {
                if ((tileIndex + 1).ToString() == tile.Content.ToString())
                {
                    return true;
                }
            }
            return false;
        }

        private Button FindButtonWithCurrentValue(int tileValue)
        {
            for (int row = 0; row < _buttons.GetLength(0); row++)
            {
                for (int col = 0; col < _buttons.GetLength(1); col++)
                {
                    if (_buttons[row, col].Content.ToString() == tileValue.ToString())
                    {
                        return _buttons[row, col];
                    }
                }
            }
            return null;
        }

        private Button FindButtonWithSovledValue(int tileValue)
        {
            int row = (tileValue - 1) / _boardSize;
            int col = (tileValue - 1) % _boardSize;
            return _buttons[row, col];
        }

        private Button FindBlankButton()
        {
            return _buttons[_blankRow, _blankCol];
        }

        private Button FindButtonLeftOf(Button source, int columnOffset)
        {
            int sourceColumn = Grid.GetColumn(source);
            int sourceRow = Grid.GetRow(source);
            int newColumn = sourceColumn - columnOffset;

            if ( newColumn >= 0 && newColumn < _boardSize)
            {
                return _buttons[sourceRow, newColumn];
            }

            return null;
        }

        private Button FindButtonBelowBy(Button source, int rowOffset)
        {
            int sourceColumn = Grid.GetColumn(source);
            int sourceRow = Grid.GetRow(source);
            int newRow = sourceRow + rowOffset;

            if (newRow >= 0 && newRow < _boardSize)
            {
                return _buttons[newRow, sourceColumn];
            }

            return null;
        }

        private Button SuggestNextMove()
        {
            if (IsSolved()) return null;

            //Check for first Unbroken 
            int nextTileToSolve = NextTileToSolve();                       

            Button sourceTile = FindButtonWithCurrentValue(nextTileToSolve);            
            Button targetTile = FindButtonWithSovledValue(nextTileToSolve); 
            Button blankTile = FindBlankButton();
            Button hintTile = null;
            // start of each row, source below target or source diagonally below target but Blank !below Target , blankTile != targetTile
            //if (nextTileToSolve % _boardSize == 0 && ( TilesAreAdjcent(targetTile,sourceTile)  || (TilesAreDiagonal(sourceTile, targetTile) && ((!TilesAreAdjcent(blankTile, sourceTile) && !TilesAreAdjcent(blankTile,targetTile)))))  && blankTile != targetTile )
            if (_boardSize > 2 && nextTileToSolve % _boardSize == 0 && (FindButtonBelowBy(targetTile, 1) == sourceTile || (TilesAreDiagonal(sourceTile, targetTile) && FindButtonBelowBy(targetTile, 1) != blankTile)) && blankTile != targetTile) // this one worked before tyring for expanded pattern
            //if (Grid.GetColumn(targetTile) >= 2 && Grid.GetColumn(targetTile) <= Grid.GetColumn(blankTile) && ( FindButtonBelowBy(targetTile,1)==sourceTile  || (TilesAreDiagonal(sourceTile, targetTile) && FindButtonBelowBy (targetTile,1) != blankTile ))&& blankTile != targetTile )
            {
                // Need to break row to solve


                //Button sourceL1 = FindButtonLeftOf(sourceTile, 1);

                int sourceOffset = 2;
                if (TilesAreDiagonal(sourceTile, targetTile))
                {
                    sourceOffset = 1;
                }
                else if (sourceTile == FindButtonLeftOf(targetTile, 1))
                {
                    return FindHintToMove(targetTile, sourceTile);
                }

                Button sourceL2 = FindButtonLeftOf(sourceTile, sourceOffset);
                Button targetL1 = FindButtonLeftOf(targetTile, 1);
                Button targetL2 = FindButtonLeftOf(targetTile, 2);

                if (targetL2.Content.ToString() == (nextTileToSolve - 2).ToString())
                {//First move Tile that is 2 columns left (targetL2) of target to location 2 columns left of source (sourceL2) //move 2 down
                    hintTile = FindHintToMove(sourceL2, targetL2);
                }
                else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == "" && targetTile.Content.ToString() != nextTileToSolve.ToString())
                {//then move tile that is 1 column left of target to location 2 columns left of target //move 3 one left
                    hintTile = FindHintToMove(targetL2, targetL1);
                }
                else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString() && targetL1.Content.ToString() == "" && targetTile.Content.ToString() != nextTileToSolve.ToString())
                {//then move targettile to location 1 columns left of target //move extra cell one left
                    if (TilesAreDiagonal(sourceTile, targetTile))
                    {
                        hintTile = FindHintToMove(targetL1, sourceTile);
                    }
                    else
                    {
                        hintTile = FindHintToMove(targetL1, targetTile);
                    }
                    
                }
                else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString() && targetL1.Content.ToString() != "" && targetTile.Content.ToString() != nextTileToSolve.ToString())
                {//finally move the sourcetile to the target location //move 4 up
                    hintTile = FindHintToMove(targetTile, sourceTile);
                }
                //else if(sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString() && targetL1.Content.ToString() != "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                //{// next move the extra cell out of the way //move extra cell one down
                //    hintTile = FindHintToMove(sourceL1, targetL1);
                //}
                //else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString()  && targetL1.Content.ToString() == "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                //{// Move one broken cell back into place //move 3 back one right
                //    hintTile = FindHintToMove( targetL1, targetL2);
                //}
                //else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                //{// move the last broken cell back into place //move 2 back up
                //    hintTile = FindHintToMove(targetL2, sourceL2);
                //}
                else
                {
                    //this shouldn't happen
                }

            }
            else if(nextTileToSolve % _boardSize == 0 && targetTile==sourceTile)
            {
                Button underTarget = FindButtonBelowBy(targetTile, 1);

                Button sourceL1 = FindButtonLeftOf(underTarget, 1);
                Button sourceL2 = FindButtonLeftOf(underTarget, 2);
                Button targetL1 = FindButtonLeftOf(targetTile, 1);
                Button targetL2 = FindButtonLeftOf(targetTile, 2);

                if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString() && targetL1.Content.ToString() != "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                {// next move the extra cell out of the way //move extra cell one down
                    hintTile = FindHintToMove(sourceL1, targetL1);
                }
                else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == (nextTileToSolve - 1).ToString() && targetL1.Content.ToString() == "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                {// Move one broken cell back into place //move 3 back one right
                    hintTile = FindHintToMove(targetL1, targetL2);
                }
                else if (sourceL2.Content.ToString() == (nextTileToSolve - 2).ToString() && targetL2.Content.ToString() == "" && targetTile.Content.ToString() == nextTileToSolve.ToString())
                {// move the last broken cell back into place //move 2 back up
                    hintTile = FindHintToMove(targetL2, sourceL2);
                }
                else
                {
                    //this shouldn't happen
                }
            }

            else
            {
                hintTile = FindHintToMove(targetTile, sourceTile);
            }
                



            if (hintTile != null)
            {
                hintTile.Background = new SolidColorBrush(Colors.Green);
            }
            return hintTile;
        }

        private Button FindHintToMove(Button targetTile, Button sourceTile)
        {
            Button blankTile = FindBlankButton();
            Button hintTile = null;

            sourceTile.Background = new SolidColorBrush(Colors.Blue);
            targetTile.Background = new SolidColorBrush(Colors.Red);

            bool blankIsAdjcent = TilesAreAdjcent(sourceTile, blankTile);
            bool blankIsInBounds = LocationIsInBoundsOfTarget(sourceTile, targetTile, blankTile);

            //Check if blank equals target and source is adjcent
            if (TilesAreAdjcent(sourceTile, targetTile) && targetTile == blankTile)
            {
                hintTile = sourceTile;
            }          
            else if (blankIsInBounds && blankIsAdjcent)
            {
                //Check if blank is adjcent and in bounds
                hintTile = sourceTile;
            }
            else if (blankIsInBounds)
            {
                //Move blank closer to source
                hintTile = FindCloserNeighborInBounds(sourceTile, targetTile, blankTile);
            }
            else if (TilesAllAdjcentInLine(targetTile, sourceTile, blankTile))
            {
                //Move blank out of line
                hintTile = MoveOutOfLineToGoAround(targetTile, sourceTile, blankTile);
            }
            else
            {
                //Move blank in bounds or to target if adjecent
                hintTile = FindNeighborCloserToBounds(sourceTile, targetTile, blankTile);
            }

            //If getting closer doesn't work try going around
            if (hintTile == null)
            {
                hintTile = MoveAwayToGoAround(sourceTile, targetTile, blankTile);
            }

            return hintTile;
        }


        private Button FindCloserNeighborInBounds(Button source, Button target, Button blank)
        {
            //Normally used for Blank, but can also be used to test any tile location
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            int neighborRow;
            int neighborColumn;

            Button neighbor;

            //TODO: rework this section so that it is tighter 
            // which conditions work for a generic neighbor? maybe both row conditions and both col conditions are same?
            //

     

            //Examine neighbor on right
            neighborColumn = blankColumn + 1;
            if (neighborColumn <= sourceColumn && Math.Abs(neighborColumn - sourceColumn) < Math.Abs(blankColumn - sourceColumn) && neighborColumn != targetColumn)
            {
                neighbor = _buttons[blankRow, neighborColumn];
                if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor,target,source)==false)
                {

                    if (LocationIsInBoundsOfTarget(source, target, neighbor))
                    {
                        return neighbor;
                    }
                }
            }

            //Examine neighbor on left
            neighborColumn = blankColumn - 1;
            if (neighborColumn >= 0 && Math.Abs(neighborColumn - sourceColumn) < Math.Abs(blankColumn - sourceColumn) && neighborColumn != targetColumn)
            {
                neighbor = _buttons[blankRow, neighborColumn];
                if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                {

                    if (LocationIsInBoundsOfTarget(source, target, neighbor))
                    {
                        return neighbor;
                    }
                }
            }

            //Examine neighbor above
            neighborRow = blankRow - 1;
            if (neighborRow >= 0 && Math.Abs(neighborRow - sourceRow) < Math.Abs(blankRow - sourceRow) && neighborRow != targetRow)
            {
                neighbor = _buttons[neighborRow, blankColumn];
                if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                {
                    if (LocationIsInBoundsOfTarget(source, target, neighbor))
                    {
                        return neighbor;
                    }
                }

            }

            //Examine neighbor below
            neighborRow = blankRow + 1;
            if (neighborRow <= sourceRow && Math.Abs(neighborRow - sourceRow) < Math.Abs(blankRow - sourceRow) && neighborRow != targetRow)
            {
                neighbor = _buttons[neighborRow, blankColumn];
                if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                {

                    if (LocationIsInBoundsOfTarget(source, target, neighbor))
                    {
                        return neighbor;
                    }
                }
            }

  

            return null;
        }

        private Button FindNeighborCloserToBounds(Button source, Button target, Button blank)
        {
            //Normally used for Blank, but can also be used to test any tile location
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            int neighborRow;
            int neighborColumn;

            Button neighbor;

            //TODO: rework this section so that it is tighter and first checks[prefers] if both are closer and if none work then if one is closer
            // which conditions work for a generic neighbor? maybe both row conditions and both col conditions are same?
            //
     
            //Test RIGHT then UP first to avoide left wall

            //Examine neighbor on right
            neighborColumn = blankColumn + 1;
            if (neighborColumn < _boardSize && !(neighborColumn == sourceColumn && blankRow == sourceRow))
            //if (neighborColumn <= sourceColumn && !(neighborColumn == sourceColumn && blankRow == sourceRow))
            {
                //Is it closer to target
                //if ( (Math.Abs(neighborColumn - sourceColumn) < Math.Abs(blankColumn - sourceColumn) && Math.Abs(neighborColumn - targetColumn) < Math.Abs(blankColumn - targetColumn)))
                if ( Math.Abs(neighborColumn - targetColumn) < Math.Abs(blankColumn - targetColumn))
                {
                    neighbor = _buttons[blankRow, neighborColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        if (!TilesAllAdjcentInLine(target, source, blank)) //Added to avoid blank moving back and forth on right edge of grid
                        {                       
                            return neighbor;
                        }
                    }
                }
              
            }

            //Examine neighbor above
            neighborRow = blankRow - 1;
            if (neighborRow >= 0 && !(neighborRow == sourceRow && blankColumn == sourceColumn))
            {
                //Is it closer to target
                //if ((Math.Abs(neighborRow - sourceRow) < Math.Abs(blankRow - sourceRow) && Math.Abs(neighborRow - targetRow) < Math.Abs(blankRow - targetRow)))
                if (Math.Abs(neighborRow - targetRow) < Math.Abs(blankRow - targetRow))
                {
                    neighbor = _buttons[neighborRow, blankColumn];
                    if (IsTileSolved(neighbor) == false && TilesAllAdjcentInLine(target, source, neighbor) == false)
                    {
                        if (IsTileProtectedForBreak(neighbor,target,source) == false)
                        {
                            return neighbor;
                        }                        
                    }
                }
            }

            //Examine neighbor on left
            neighborColumn = blankColumn - 1;
            if (neighborColumn >= 0 && !(neighborColumn == sourceColumn && blankRow == sourceRow))            
            {
                //Is it closer to target
                //if ((Math.Abs(neighborColumn - sourceColumn) < Math.Abs(blankColumn - sourceColumn) && Math.Abs(neighborColumn - targetColumn) < Math.Abs(blankColumn - targetColumn)))
                if ( Math.Abs(neighborColumn - targetColumn) < Math.Abs(blankColumn - targetColumn))
                {
                    neighbor = _buttons[blankRow, neighborColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
                }
            }

        

            //Examine neighbor below
            neighborRow = blankRow + 1;
            if (neighborRow < _boardSize && !(neighborRow == sourceRow && blankColumn == sourceColumn))
            //if (neighborRow <= sourceRow && !(neighborRow == sourceRow && blankColumn == sourceColumn))
            {
                //Is it closer to target                
                if (Math.Abs(neighborRow - targetRow) < Math.Abs(blankRow - targetRow))
                {
                    neighbor = _buttons[neighborRow, blankColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
                }
            }

            return null;
        }

        private Button MoveOutOfLineToGoAround(Button target, Button source, Button blank)
        {
            //Specifically for use when Target, Source and Blank are all in a row in that order
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            int neighborRow;
            int neighborColumn;

            Button neighbor;

            if (targetRow == sourceRow && sourceRow == blankRow)
            {//Aligned horizontally 

                //Examine neighbor below
                neighborRow = blankRow + 1;
                if (neighborRow < _boardSize)
                {
                    neighbor = _buttons[neighborRow, blankColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
                }

            }
            else if (targetColumn == sourceColumn && sourceColumn == blankColumn)
            {//Aligned vertically

                //Examine neighbor on right
                neighborColumn = blankColumn + 1;
                if (neighborColumn < _boardSize)
                {
                    neighbor = _buttons[blankRow, neighborColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
                }

                //Examine neighbor on left
                neighborColumn = blankColumn - 1;
                if (neighborColumn >= 0)
                {
                    neighbor = _buttons[blankRow, neighborColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
                }

            }

           

          

            return null;
        }

        private Button MoveAwayToGoAround(Button source, Button target, Button blank)
        {
            //Normally used for Blank, but can also be used to test any tile location
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            int neighborRow;
            int neighborColumn;

            Button neighbor;


            //Examine neighbor on right
            neighborColumn = blankColumn + 1;
            if (neighborColumn < _boardSize && !(neighborColumn == sourceColumn && blankRow == sourceRow))            
            {
                    neighbor = _buttons[blankRow, neighborColumn];
                    if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                    {
                        return neighbor;
                    }
            }

            //Examine neighbor below
            neighborRow = blankRow + 1;
            if (neighborRow<_boardSize && !(neighborRow == sourceRow && blankColumn == sourceColumn))            
            {                               
                neighbor = _buttons[neighborRow, blankColumn];
                if (IsTileSolved(neighbor) == false && IsTileProtectedForBreak(neighbor, target, source) == false)
                {
                    return neighbor;
                }
            }

            return null;
        }

        private bool TilesAreAdjcent(Button source, Button target)
        {
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            if (source == target) return false;

            if (sourceRow == targetRow)
            {
                if (sourceColumn - 1 == targetColumn || sourceColumn == targetColumn - 1) return true;
            }
            else if (sourceColumn == targetColumn)
            {
                if (sourceRow - 1 == targetRow || sourceRow == targetRow - 1) return true;
            }
            return false;
        }

        private bool TilesAreDiagonal(Button source, Button target)
        {//Order matters - source must come first
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            if (source == target) return false;

            if (sourceRow == targetRow + 1)//Source must be below
            {
                //if (sourceColumn - 1 == targetColumn || sourceColumn == targetColumn - 1) return true;
                if  (sourceColumn == targetColumn - 1) return true;//Source must be to left
            }
            return false;
        }

        private bool TilesAllAdjcentInLine( Button target, Button source, Button blank)
        {
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            if (source == target || blank == target) return false;

            if ( targetRow == sourceRow && sourceRow == blankRow)
            {
                if (targetColumn <= sourceColumn - 1 && sourceColumn <= blankColumn - 1) return true;
            }
            else if (targetColumn == sourceColumn && sourceColumn == blankColumn)
            {
                if (targetRow <= sourceRow - 1 && sourceRow <= blankRow - 1) return true;
            }
            return false;

        }

        private bool LocationIsInBoundsOfTarget(Button source, Button target, Button blank)
        {
            //Normally used for Blank, but can also be used to test any tile location
            int sourceRow = Grid.GetRow(source);
            int sourceColumn = Grid.GetColumn(source);

            int targetRow = Grid.GetRow(target);
            int targetColumn = Grid.GetColumn(target);

            int blankRow = Grid.GetRow(blank);
            int blankColumn = Grid.GetColumn(blank);

            if (targetRow <= sourceRow)
            {
                if (targetColumn < sourceColumn)
                {
                    //Source on the right
                    if (blankColumn >= targetColumn && blankColumn <= sourceColumn && blankRow >= targetRow && blankRow <= sourceRow)
                    {
                        return true;
                    }
                    return false;
                }
                else if (targetColumn == sourceColumn)
                {
                    //Alligned
                    if (blankColumn == sourceColumn && blankRow >= targetRow && blankRow <= sourceRow)
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    //Source on the left
                    if (blankColumn <= targetColumn && blankColumn >= sourceColumn && blankRow >= targetRow && blankRow <= sourceRow)
                    {
                        return true;
                    }
                    return false;
                }
            }
            else
            {
                //only happens on last row target items under managed break condition
                //TODO: Handle this scenario correctly
            }

            return false;
        }

        private void OnAskForHint(object sender, RoutedEventArgs e)
        {            
            SuggestNextMove();            
        }

        #endregion

        private void OnMoveHint(object sender, RoutedEventArgs e)
        {                        
            Button NextMove = SuggestNextMove();
            if (NextMove != null)
            {
                OnButtonClick(NextMove, new RoutedEventArgs());
            }            
        }
    }
}

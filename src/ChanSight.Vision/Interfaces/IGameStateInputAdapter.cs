using ChanSight.Vision.Models;

namespace ChanSight.Vision.Interfaces;

public interface IGameStateInputAdapter
{
    void Apply(RecognitionFrame frame);
}
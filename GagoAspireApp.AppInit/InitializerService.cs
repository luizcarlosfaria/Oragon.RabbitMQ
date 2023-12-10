using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GagoAspireApp.AppInit;

public class InitializerService: IHostedService
{
    public InitializerService()
    {
    
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {


        return Task.Delay(TimeSpan.FromSeconds(10));
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {

        return Task.CompletedTask;
    }

    
}
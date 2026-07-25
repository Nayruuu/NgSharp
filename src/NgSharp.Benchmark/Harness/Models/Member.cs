using System;
using System.Collections.Generic;

namespace NgSharp.Benchmark;

public sealed class Member
{
    public string Name { get; set; }

    public string Role { get; set; }

    public decimal Salary { get; set; }

    public int Age { get; set; }

    public bool IsLead { get; set; }

    public bool IsRemote { get; set; }

    public DateTime JoinedAt { get; set; }

    public string StatusHtml { get; set; }

    public string RemoteLabel { get; set; }

    public string OnsiteLabel { get; set; }

    public List<TaskItem> Tasks { get; set; }
}

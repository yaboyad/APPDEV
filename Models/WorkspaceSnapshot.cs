using System.Collections.Generic;

namespace Label_CRM_demo.Models;

public sealed record WorkspaceSnapshot(
    IReadOnlyList<ContactRecord> Contacts,
    IReadOnlyList<ContractRecord> Contracts);

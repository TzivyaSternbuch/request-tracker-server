using RequestTracker.Core.Domain;

namespace RequestTracker.Infrastructure.Seeding;

// Word lists and weights for generated requests. Titles and organization names are combined from these parts.
internal static class SeedData
{
    public static readonly string[] TitleActions =
    [
        "Replace broken printer",
        "Reset VPN access",
        "Renew annual software license",
        "Install accounting software",
        "Repair leaking air conditioner",
        "Upgrade laptop memory",
        "Restore deleted shared folder",
        "Set up new workstation",
        "Fix email sync on mobile phone",
        "Order replacement monitor",
        "Unlock locked user account",
        "Configure meeting room projector",
        "Replace faulty network switch",
        "Grant access to finance reports",
        "Remove malware from desktop",
        "Move phone extension",
        "Recover lost database backup",
        "Update antivirus definitions",
        "Repair badge reader at entrance",
        "Increase mailbox storage quota",
        "Replace worn office chairs",
        "Fix slow Wi-Fi connection",
        "Schedule fire alarm inspection",
        "Order toner cartridges",
        "Clean up old user accounts",
        "Investigate failed payroll export",
        "Install security camera",
        "Renew SSL certificate",
        "Repair broken window blinds",
        "Migrate files to cloud storage",
        "Set up video conferencing",
        "Fix printing errors in invoices",
        "Replace dead UPS battery",
        "Create shared calendar",
        "Reset forgotten password",
        "Repair elevator door sensor",
        "Add new employee to mailing list",
        "Replace cracked phone screen",
        "Fix broken door lock",
        "Back up project archive",
    ];

    public static readonly string[] TitleContexts =
    [
        "on 3rd floor",
        "for new employee",
        "in finance department",
        "at main office",
        "in warehouse",
        "for sales team",
        "in reception area",
        "at branch office",
        "for management",
        "in server room",
        "before quarterly audit",
        "in training room",
        "for remote workers",
        "in HR department",
        "at north entrance",
        "for customer support",
        "in conference room B",
        "before month end",
        "in marketing department",
        "for summer interns",
        "in staff kitchen",
        "at loading dock",
        "for IT helpdesk",
        "in basement storage",
        "for night shift",
    ];

    public static readonly string[] OrganizationNames =
    [
        "Northwind", "Harbor", "Bluebird", "Summit", "Riverside", "Oakwood", "Silverline", "Greenfield", "Lakeside",
        "Pinecrest", "Redstone", "Clearwater", "Highland", "Westbrook", "Brightway", "Ironbridge", "Maplewood",
        "Starlight", "Evergreen", "Crescent",
    ];

    public static readonly string[] OrganizationTypes =
    [
        "Logistics", "Medical Center", "Bank", "Insurance", "Municipality", "University", "Engineering",
        "Pharmaceuticals", "Retail Group", "Law Firm", "Construction", "Energy", "Foods", "Hotels", "Software",
    ];

    public static readonly string[] Assignees =
    [
        "Avi Cohen", "Noa Levi", "Daniel Mizrahi", "Yael Peretz", "Michael Friedman", "Tamar Katz", "David Azoulay",
        "Shira Biton", "Jonathan Weiss", "Maya Dahan", "Eitan Shapiro", "Rachel Goldberg", "Omer Ben-David",
        "Hila Avraham", "Yosef Klein", "Adi Ohana", "Ariel Rosen", "Liat Malka", "Benjamin Stern", "Neta Gabay",
        "Sarah Miller", "James Carter", "Emily Johnson", "Robert Brown", "Olivia Davis", "William Wilson",
        "Sophia Moore", "Thomas Taylor", "Emma Anderson", "Lucas Martin", "Chloe Thompson", "Ethan White",
        "Mia Harris", "Nathan Clark", "Ava Lewis", "Samuel Walker", "Grace Hall", "Henry Young", "Lily King",
        "Jacob Wright",
    ];

    // Most requests are Completed, so the status indexes see realistic selectivity (task 17).
    public static readonly (RequestStatus Value, int Weight)[] StatusWeights =
    [
        (RequestStatus.Completed, 70),
        (RequestStatus.InProgress, 12),
        (RequestStatus.New, 10),
        (RequestStatus.Waiting, 8),
    ];

    public static readonly (RequestPriority Value, int Weight)[] PriorityWeights =
    [
        (RequestPriority.Medium, 50),
        (RequestPriority.Low, 30),
        (RequestPriority.High, 20),
    ];
}

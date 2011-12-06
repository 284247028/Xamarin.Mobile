using System;
using System.Collections.Generic;
using Android.Content;
using Android.Content.Res;
using Android.Database;
using Android.Provider;

using StructuredName = Android.Provider.ContactsContract.CommonDataKinds.StructuredName;
using StructuredPostal = Android.Provider.ContactsContract.CommonDataKinds.StructuredPostal;
using CommonColumns = Android.Provider.ContactsContract.CommonDataKinds.CommonColumns;
using Uri = Android.Net.Uri;

namespace Xamarin.Contacts
{
	internal static class ContactHelper
	{
		internal static IEnumerable<Contact> GetContacts (bool rawContacts, ContentResolver content, Resources resources)
		{
			ICursor c = null;

			Uri curi = (rawContacts)
						? ContactsContract.RawContacts.ContentUri
						: ContactsContract.Contacts.ContentUri;

			try
			{
				c = content.Query (curi, null, null, null, null);
				while (c.MoveToNext())
					yield return GetContact (rawContacts, content, resources, c);
			}
			finally
			{
				if (c != null)
					c.Close();
			}
		}

		internal static Contact GetContact (bool rawContact, ContentResolver content, Resources resources, ICursor cursor)
		{
			string id = (rawContact)
							? cursor.GetString (cursor.GetColumnIndex (ContactsContract.RawContactsColumns.ContactId))
							: cursor.GetString (cursor.GetColumnIndex (ContactsContract.ContactsColumns.LookupKey));

			Contact contact = new Contact (id, !rawContact, content);
			contact.DisplayName = GetString (cursor, ContactsContract.ContactsColumns.DisplayName);

			FillContactExtras (rawContact, content, resources, id, contact);

			return contact;
		}

		internal static void FillContactExtras (bool rawContact, ContentResolver content, Resources resources, string recordId, Contact contact)
		{
			ICursor c = null;

			List<Address> addresses = new List<Address>();
			List<Phone> phones = new List<Phone>();
			List<Email> emails = new List<Email>();
			List<string> notes = new List<string>();
			List<Organization> organizations = new List<Organization>();

			string column = (rawContact)
								? ContactsContract.RawContactsColumns.ContactId
								: ContactsContract.ContactsColumns.LookupKey;

			try
			{
				c = content.Query (ContactsContract.Data.ContentUri, null, column + " = ?", new[] { recordId }, null);
				while (c.MoveToNext())
				{
					string dataType = c.GetString (c.GetColumnIndex (ContactsContract.DataColumns.Mimetype));
					switch (dataType)
					{
						case ContactsContract.CommonDataKinds.Nickname.ContentItemType:
							contact.Nickname = c.GetString (c.GetColumnIndex (ContactsContract.CommonDataKinds.Nickname.Name));
							break;

						case StructuredName.ContentItemType:
							contact.Prefix = c.GetString (StructuredName.Prefix);
							contact.FirstName = c.GetString (StructuredName.GivenName);
							contact.MiddleName = c.GetString (StructuredName.MiddleName);
							contact.LastName = c.GetString (StructuredName.FamilyName);
							contact.Suffix = c.GetString (StructuredName.Suffix);
							break;

						case ContactsContract.CommonDataKinds.Phone.ContentItemType:
							Phone p = new Phone();
							p.Number = GetString (c, ContactsContract.CommonDataKinds.Phone.Number);

							PhoneDataKind pkind = (PhoneDataKind)c.GetInt (c.GetColumnIndex (CommonColumns.Type));
							p.Type = pkind.ToPhoneType();
							p.Label = ContactsContract.CommonDataKinds.Phone.GetTypeLabel (resources, pkind, c.GetString (CommonColumns.Label));

							phones.Add (p);
							break;

						case ContactsContract.CommonDataKinds.Email.ContentItemType:
							Email e = new Email();
							e.Address = c.GetString (ContactsContract.DataColumns.Data1);

							EmailDataKind ekind = (EmailDataKind)c.GetInt (c.GetColumnIndex (CommonColumns.Type));
							e.Type = ekind.ToEmailType();
							e.Label = ContactsContract.CommonDataKinds.Email.GetTypeLabel (resources, ekind, c.GetString (CommonColumns.Label));

							emails.Add (e);
							break;

						case ContactsContract.CommonDataKinds.Note.ContentItemType:
							notes.Add (GetString (c, ContactsContract.CommonDataKinds.Note.NoteColumnId));
							break;

						case ContactsContract.CommonDataKinds.Organization.ContentItemType:
							Organization o = new Organization();
							o.Name = c.GetString (ContactsContract.CommonDataKinds.Organization.Company);
							o.ContactTitle = c.GetString (ContactsContract.CommonDataKinds.Organization.Title);
							
							OrganizationDataKind d = (OrganizationDataKind)c.GetInt (c.GetColumnIndex (ContactsContract.CommonDataKinds.CommonColumns.Type));
							o.Type = d.ToOrganizationType();
							o.Label = ContactsContract.CommonDataKinds.Organization.GetTypeLabel (resources, d, GetString (c, ContactsContract.CommonDataKinds.CommonColumns.Label));

						case StructuredPostal.ContentItemType:
							addresses.Add (GetAddress (c, resources));
							break;

							break;
					}
				}

				contact.Phones = phones;
				contact.Emails = emails;
				contact.Notes = notes;
				contact.Organizations = organizations;
				contact.Addresses = addresses;
			}
			finally
			{
				if (c != null)
					c.Close();
			}
		}


		private static Address GetAddress (ICursor c, Resources resources)
		{
			Address a = new Address();
			a.Country = c.GetString (StructuredPostal.Country);
			a.Region = c.GetString (StructuredPostal.Region);
			a.City = c.GetString (StructuredPostal.City);
			a.PostalCode = c.GetString (StructuredPostal.Postcode);

			AddressDataKind kind = (AddressDataKind) c.GetInt (c.GetColumnIndex (CommonColumns.Type));
			a.Type = kind.ToAddressType();
			a.Label = (kind != AddressDataKind.Custom)
						? StructuredPostal.GetTypeLabel (resources, kind, String.Empty)
						: c.GetString (CommonColumns.Label);

			string street = c.GetString (StructuredPostal.Street);
			string pobox = c.GetString (StructuredPostal.Pobox);
			if (street != null)
				a.StreetAddress = street;
			if (pobox != null)
			{
				if (street != null)
					a.StreetAddress += Environment.NewLine;

				a.StreetAddress += pobox;
			}
			return a;
		}

		private static Phone GetPhone (ICursor c, Resources resources)
		{
			Phone p = new Phone();
			p.Number = GetString (c, ContactsContract.CommonDataKinds.Phone.Number);

			PhoneDataKind pkind = (PhoneDataKind) c.GetInt (c.GetColumnIndex (CommonColumns.Type));
			p.Type = pkind.ToPhoneType();
			p.Label = (pkind != PhoneDataKind.Custom)
						? ContactsContract.CommonDataKinds.Phone.GetTypeLabel (resources, pkind, String.Empty)
						: c.GetString (CommonColumns.Label);

			return p;
		}

		private static Email GetEmail (ICursor c, Resources resources)
		{
			Email e = new Email();
			e.Address = c.GetString (ContactsContract.DataColumns.Data1);

			EmailDataKind ekind = (EmailDataKind) c.GetInt (c.GetColumnIndex (CommonColumns.Type));
			e.Type = ekind.ToEmailType();
			e.Label = (ekind != EmailDataKind.Custom)
						? ContactsContract.CommonDataKinds.Email.GetTypeLabel (resources, ekind, String.Empty)
						: c.GetString (CommonColumns.Label);

			return e;
		}

		private static Organization GetOrganization (ICursor c, Resources resources)
		{
			Organization o = new Organization();
			o.Name = c.GetString (ContactsContract.CommonDataKinds.Organization.Company);
			o.ContactTitle = c.GetString (ContactsContract.CommonDataKinds.Organization.Title);

			OrganizationDataKind d = (OrganizationDataKind) c.GetInt (c.GetColumnIndex (CommonColumns.Type));
			o.Type = d.ToOrganizationType();
			o.Label = (d != OrganizationDataKind.Custom)
						? ContactsContract.CommonDataKinds.Organization.GetTypeLabel (resources, d, String.Empty)
						: c.GetString (CommonColumns.Label);

			return o;
		}

		internal static string GetString (this ICursor c, string colName)
		{
			return c.GetString (c.GetColumnIndex (colName));
		}

		internal static AddressType ToAddressType (this AddressDataKind addressKind)
		{
			switch (addressKind)
			{
				case AddressDataKind.Home:
					return AddressType.Home;
				case AddressDataKind.Work:
					return AddressType.Work;
				default:
					return AddressType.Other;
			}
		}

		internal static EmailType ToEmailType (this EmailDataKind emailKind)
		{
			switch (emailKind)
			{
				case EmailDataKind.Home:
					return EmailType.Home;
				case EmailDataKind.Work:
					return EmailType.Work;
				default:
					return EmailType.Other;
			}
		}

		internal static PhoneType ToPhoneType (this PhoneDataKind phoneKind)
		{
			switch (phoneKind)
			{
				case PhoneDataKind.Home:
					return PhoneType.Home;
				case PhoneDataKind.Mobile:
					return PhoneType.Mobile;
				case PhoneDataKind.FaxHome:
					return PhoneType.HomeFax;
				case PhoneDataKind.Work:
					return PhoneType.Work;
				case PhoneDataKind.FaxWork:
					return PhoneType.WorkFax;
				case PhoneDataKind.Pager:
				case PhoneDataKind.WorkPager:
					return PhoneType.Pager;
				default:
					return PhoneType.Other;
			}
		}

		internal static OrganizationType ToOrganizationType (this OrganizationDataKind organizationKind)
		{
			switch (organizationKind)
			{
				case OrganizationDataKind.Work:
					return OrganizationType.Work;

				default:
					return OrganizationType.Other;
			}
		}
	}
}